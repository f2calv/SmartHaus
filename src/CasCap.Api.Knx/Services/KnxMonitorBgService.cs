using Knx.Falcon;
using Knx.Falcon.Configuration;
using Knx.Falcon.KnxnetIp;
using Knx.Falcon.Sdk;

namespace CasCap.Services;

/// <summary>
/// Connects to the KNX bus and receives, parses and then publishes telegrams via the
/// <see cref="IKnxTelegramBroker{T}"/> for <see cref="KnxEvent"/>.
/// When a line connection drops, it is automatically re-established without affecting other lines.
/// </summary>
public class KnxMonitorBgService(
    ILogger<KnxMonitorBgService> logger,
    IOptions<RedlockConfig> redlockConfig,
    IOptions<KnxConfig> config,
    KnxGroupAddressLookupService knxGroupAddressLookupSvc,
    KnxGroupAddressLookupHealthCheck knxGroupAddressLookupHealthCheck,
    KnxConnectionHealthCheck knxConnectionHealthCheck,
    IKnxTelegramBroker<KnxEvent> incomingBroker,
    KnxConnectionNotifier connectionNotifier,
    IDistributedLockFactory lockFactory) : IBgFeature
{

    /// <summary>
    /// Discovery parameters per area/line, retained for reconnection without re-discovery.
    /// </summary>
    private readonly Dictionary<KnxAreaLine, List<IpTunnelingConnectorParameters>> _lineParameters = [];

    /// <summary>
    /// Channel used by <see cref="Bus_ConnectionStateChanged"/> to signal that a line needs reconnection.
    /// </summary>
    private readonly Channel<KnxAreaLine> _reconnectChannel = Channel.CreateUnbounded<KnxAreaLine>(
        new UnboundedChannelOptions { SingleReader = true });

    /// <summary>
    /// All discovered tunneling interface individual addresses (including slots occupied by other deployments).
    /// Telegrams whose source is in this set but not in <see cref="_connectedAddresses"/> are dropped.
    /// </summary>
    private readonly HashSet<string> _allDiscoveredAddresses = [];

    /// <summary>
    /// Individual addresses of our active tunneling connections (e.g. "1.1.2").
    /// Thread-safe because it is mutated on the connection management path and read from event handler threads.
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _connectedAddresses = new();

    /// <inheritdoc/>
    public string FeatureName => "Knx";

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} starting", nameof(KnxMonitorBgService));
        try
        {
            //load group addresses before entering leadership election — this is a stateless,
            //read-only operation that all replicas can perform concurrently
            await WaitForGroupAddressLookupAsync(cancellationToken);

            logger.LogInformation("{ClassName} ServiceFamily={ServiceFamily}, ShardingMode={ShardingMode}, TelegramBrokerMode={TelegramBrokerMode}",
                nameof(KnxMonitorBgService), config.Value.ServiceFamily, config.Value.ShardingMode, config.Value.TelegramBrokerMode);

            //routing uses a single multicast connection — partitioned sharding is not applicable
            var effectiveShardingMode = config.Value.ServiceFamily == ServiceFamily.Routing
                ? ShardingMode.Unified
                : config.Value.ShardingMode;

            if (effectiveShardingMode != config.Value.ShardingMode)
                logger.LogWarning("{ClassName} ShardingMode overridden to {EffectiveShardingMode} because ServiceFamily is {ServiceFamily}",
                    nameof(KnxMonitorBgService), effectiveShardingMode, config.Value.ServiceFamily);

            switch (effectiveShardingMode)
            {
                case ShardingMode.Unified:
                    await AcquireLock(cancellationToken);
                    break;
                case ShardingMode.Partitioned:
                    throw new NotSupportedException(
                        $"{nameof(KnxConfig.ShardingMode)} '{nameof(ShardingMode.Partitioned)}' is not yet supported. " +
                        $"Use {nameof(ShardingMode.Unified)}.");
                default:
                    throw new NotSupportedException(
                        $"{nameof(KnxConfig.ShardingMode)} '{effectiveShardingMode}' is not supported.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException) { throw; }
        logger.LogInformation("{ClassName} exiting", nameof(KnxMonitorBgService));
    }

    /// <summary>
    /// Loads the group address XML lookup and polls until the health check reports ready.
    /// Safe to run on all replicas concurrently before leadership election.
    /// </summary>
    private async Task WaitForGroupAddressLookupAsync(CancellationToken cancellationToken)
    {
        await knxGroupAddressLookupSvc.GetLookup(cancellationToken);

        var attempt = 1;
        while (!cancellationToken.IsCancellationRequested && !knxGroupAddressLookupHealthCheck.GroupAddressesLoaded)
        {
            logger.Log(attempt % config.Value.ConnectionLogEscalationInterval == 0 ? LogLevel.Warning : LogLevel.Trace,
                "{ClassName} group address lookup not yet loaded, attempt {Attempt}, retry in {RetryMs}ms...",
                nameof(KnxMonitorBgService), attempt, config.Value.ConnectionPollingDelayMs);
            await Task.Delay(config.Value.ConnectionPollingDelayMs, cancellationToken);
            attempt++;
        }
    }

    /// <summary>
    /// Acquires a distributed Redlock and runs the service while the lock is held.
    /// If the lock cannot be acquired (another replica is active), retries until cancellation.
    /// </summary>
    private async Task AcquireLock(CancellationToken cancellationToken)
    {
        var resource = "knx:monitor";
        var (expiry, wait, retry) = redlockConfig.Value.GetTimings(RedlockProfiles.LeaderElection);

        var lockAttempt = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            await using (var redLock = await lockFactory.CreateLockAsync(resource, expiry, wait, retry, cancellationToken))
            {
                if (redLock.IsAcquired)
                {
                    knxConnectionHealthCheck.IsLeader = true;
                    logger.LogInformation("{ClassName} distributed lock acquired on attempt {Attempt}",
                        nameof(KnxMonitorBgService), lockAttempt);
                    await RunServiceAsync(cancellationToken);
                    knxConnectionHealthCheck.IsLeader = false;
                    knxConnectionHealthCheck.ConnectionActive = false;
                    logger.LogInformation("{ClassName} distributed lock released", nameof(KnxMonitorBgService));
                }
                else
                {
                    lockAttempt++;
                    logger.LogInformation("{ClassName} distributed lock failed on attempt {Attempt}",
                        nameof(KnxMonitorBgService), lockAttempt);
                }
            }
            logger.LogInformation("{ClassName} distributed lock pending attempt {Attempt}",
                nameof(KnxMonitorBgService), lockAttempt + 1);
        }
    }

    private Task RunServiceAsync(CancellationToken cancellationToken)
        => ConnectAndMonitor(cancellationToken);

    private async Task ConnectAndMonitor(CancellationToken cancellationToken)
    {
        switch (config.Value.ServiceFamily)
        {
            case ServiceFamily.Tunneling:
                await ConnectAndMonitorTunneling(cancellationToken);
                break;
            case ServiceFamily.Routing:
                await ConnectAndMonitorRouting(cancellationToken);
                break;
            default:
                throw new NotSupportedException(
                    $"{nameof(KnxConfig.ServiceFamily)} '{config.Value.ServiceFamily}' is not supported. " +
                    $"Use {nameof(ServiceFamily.Tunneling)} or {nameof(ServiceFamily.Routing)}.");
        }
    }

    private async Task ConnectAndMonitorTunneling(CancellationToken cancellationToken)
    {
        //gather information on possible Areas+Lines
        var ipTunnelingConnectorParametersAll = await GetIpTunnelingConnectorParameters(cancellationToken);

        //group discovered parameters by area/line and store for reconnection
        _lineParameters.Clear();
        foreach (var parameters in ipTunnelingConnectorParametersAll)
        {
            var areaLine = new KnxAreaLine
            {
                Area = parameters.IndividualAddress!.Value.AreaAddress,
                Line = parameters.IndividualAddress.Value.LineAddress
            };
            if (!_lineParameters.TryGetValue(areaLine, out var list))
            {
                list = [];
                _lineParameters[areaLine] = list;
            }
            list.Add(parameters);
        }

        //compute the set of all discovered tunneling addresses for source filtering
        _allDiscoveredAddresses.Clear();
        _connectedAddresses.Clear();
        foreach (var parameters in _lineParameters.Values.SelectMany(list => list))
            if (parameters.IndividualAddress.HasValue)
                _allDiscoveredAddresses.Add(parameters.IndividualAddress.Value.ToString());

        //populate BusConnections with null entries for each discovered area/line
        KnxStatics.BusConnections.Clear();
        foreach (var areaLine in _lineParameters.Keys)
            KnxStatics.BusConnections[areaLine] = null;

        //attempt initial connection to each area/line
        foreach (var areaLine in _lineParameters.Keys)
            await ConnectLine(areaLine, cancellationToken);

        //run the health check monitor and reconnection handler concurrently;
        //await-await-WhenAny propagates the first faulted task immediately so the
        //service crashes and the pod restarts rather than running in a degraded state.
        await await Task.WhenAny(
            MonitorHealthAsync(cancellationToken),
            ProcessReconnectionRequestsAsync(cancellationToken));
    }

    private async Task ConnectAndMonitorRouting(CancellationToken cancellationToken)
    {
        var routingParameters = await GetIpRoutingConnectorParameters(cancellationToken);

        //routing uses a single multicast connection covering the entire KNX backbone
        var routingAreaLine = new KnxAreaLine { Area = 0, Line = 0 };
        KnxStatics.BusConnections.Clear();
        KnxStatics.BusConnections[routingAreaLine] = null;

        var bus = await TryConnectRouting(routingParameters, cancellationToken);
        if (bus is not null)
            KnxStatics.BusConnections[routingAreaLine] = bus;

        await MonitorHealthAsync(cancellationToken);
    }

    /// <summary>
    /// Periodically polls all bus connections and updates the <see cref="KnxConnectionHealthCheck"/>.
    /// </summary>
    private async Task MonitorHealthAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var allConnected = KnxStatics.BusConnections.Values
                .All(bus => bus?.ConnectionState == BusConnectionState.Connected);
            knxConnectionHealthCheck.ConnectionActive = allConnected;
            await Task.Delay(config.Value.ConnectionHealthPollingDelayMs, cancellationToken);
        }
    }

    /// <summary>
    /// Reads <see cref="KnxAreaLine"/> reconnection requests from <see cref="_reconnectChannel"/>
    /// and re-establishes the dropped connection, with exponential back-off.
    /// </summary>
    private async Task ProcessReconnectionRequestsAsync(CancellationToken cancellationToken)
    {
        await foreach (var areaLine in _reconnectChannel.Reader.ReadAllAsync(cancellationToken))
        {
            logger.LogWarning("{ClassName} reconnection requested for {AreaLine}", nameof(KnxMonitorBgService), areaLine);

            var backoffMs = config.Value.ReconnectBackoffMs;
            var maxBackoffMs = config.Value.ReconnectMaxBackoffMs;
            var connected = false;
            while (!connected && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(backoffMs, cancellationToken);
                connected = await ConnectLine(areaLine, cancellationToken);
                if (!connected)
                {
                    logger.LogWarning("{ClassName} reconnection to {AreaLine} failed, retrying in {BackoffMs}ms",
                        nameof(KnxMonitorBgService), areaLine, backoffMs);
                    backoffMs = Math.Min(backoffMs * 2, maxBackoffMs);
                }
            }

            if (connected)
            {
                logger.LogInformation("{ClassName} reconnection to {AreaLine} succeeded", nameof(KnxMonitorBgService), areaLine);
                connectionNotifier.Notify(new KnxConnectionStateChange
                {
                    AreaLine = areaLine,
                    Connected = true,
                    TimestampUtc = DateTime.UtcNow,
                });
            }
        }
    }

    /// <summary>
    /// Attempts to connect to a single area/line using the stored discovery parameters.
    /// Disposes any existing bus connection and unwires event handlers before reconnecting.
    /// </summary>
    /// <returns><see langword="true"/> if a connection was established; otherwise <see langword="false"/>.</returns>
    private async Task<bool> ConnectLine(KnxAreaLine areaLine, CancellationToken cancellationToken)
    {
        if (!_lineParameters.TryGetValue(areaLine, out var parametersList))
        {
            logger.LogError("{ClassName} no stored discovery parameters for {AreaLine}", nameof(KnxMonitorBgService), areaLine);
            return false;
        }

        logger.LogInformation("{ClassName} connecting to {AreaLine}", nameof(KnxMonitorBgService), areaLine);

        //dispose the old bus connection if one exists
        if (KnxStatics.BusConnections.TryGetValue(areaLine, out var oldBus) && oldBus is not null)
        {
            _connectedAddresses.TryRemove(oldBus.InterfaceConfiguration.IndividualAddress.ToString(), out _);
            DisconnectBus(oldBus);
            KnxStatics.BusConnections[areaLine] = null;
        }

        //iterate over the individual addresses and attempt a connection
        foreach (var parameters in parametersList)
        {
            var bus = await TryConnect(parameters, cancellationToken);
            if (bus is not null)
            {
                KnxStatics.BusConnections[areaLine] = bus;
                _connectedAddresses[parameters.IndividualAddress!.Value.ToString()] = 0;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Attempts a single connection to the given tunneling connector parameters.
    /// </summary>
    private async Task<KnxBus?> TryConnect(IpTunnelingConnectorParameters parameters, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return null;
        var bus = new KnxBus(parameters);
        logger.LogInformation("{ClassName} connecting to {Name} {HostAddress} ({IndividualAddress})...",
            nameof(KnxMonitorBgService), parameters.Name, parameters.HostAddress, parameters.IndividualAddress);
        try
        {
            await bus.ConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning("{ClassName} connection to {Name} {HostAddress} ({IndividualAddress}) failed, {Reason}",
                nameof(KnxMonitorBgService), parameters.Name, parameters.HostAddress, parameters.IndividualAddress,
                ex.InnerException?.Message ?? ex.Message);
            await bus.DisposeAsync();
            return null;
        }

        if (bus.ConnectionState != BusConnectionState.Connected)
        {
            await bus.DisposeAsync();
            return null;
        }

        logger.LogInformation("{ClassName} connection to {Name} {HostAddress} ({IndividualAddress}) succeeded!",
            nameof(KnxMonitorBgService), parameters.Name, parameters.HostAddress, parameters.IndividualAddress);

        //wire-up event handlers
        bus.InterfaceConfigurationChanged += Bus_InterfaceConfigurationChanged;
        bus.GroupMessageReceived += Bus_GroupMessageReceived;
        bus.ConnectionStateChanged += Bus_ConnectionStateChanged;

        logger.LogTrace("{ClassName} InterfaceFeatures {@InterfaceFeatures}", nameof(KnxMonitorBgService), bus.InterfaceFeatures);
        logger.LogTrace("{ClassName} InterfaceConfiguration {@InterfaceConfiguration}", nameof(KnxMonitorBgService), bus.InterfaceConfiguration);

        return bus;
    }

    /// <summary>
    /// Unwires event handlers and disposes a <see cref="KnxBus"/> instance.
    /// </summary>
    private void DisconnectBus(KnxBus bus)
    {
        bus.InterfaceConfigurationChanged -= Bus_InterfaceConfigurationChanged;
        bus.GroupMessageReceived -= Bus_GroupMessageReceived;
        bus.ConnectionStateChanged -= Bus_ConnectionStateChanged;
        try
        {
            bus.Dispose();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{ClassName} error disposing bus {IndividualAddress}",
                nameof(KnxMonitorBgService), bus.InterfaceConfiguration.IndividualAddress);
        }
    }

    private async Task<List<IpTunnelingConnectorParameters>> GetIpTunnelingConnectorParameters(CancellationToken cancellationToken)
    {
        var attempt = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("{ClassName} start discovery of KNX IP devices...", nameof(KnxMonitorBgService));
            var ipDeviceDiscoveryResults = await KnxBus.DiscoverIpDevicesAsync(cancellationToken)
                .Where(p => p.Supports(config.Value.ServiceFamily, 1), cancellationToken)
                .ToArray(cancellationToken);

            if (ipDeviceDiscoveryResults.Length == 0)
            {
                logger.Log(attempt % 10 == 0 ? LogLevel.Warning : LogLevel.Debug,
                    "{ClassName} no KNX IP devices detected, attempt '{Attempt}', retry in {RetryMs}ms...",
                    nameof(KnxMonitorBgService), attempt, config.Value.DiscoveryRetryDelayMs);
                await Task.Delay(config.Value.DiscoveryRetryDelayMs, cancellationToken);
                attempt++;
                continue;
            }

            var index = 1;
            var allowedParameters = new List<IpTunnelingConnectorParameters>();
            var allParameters = ipDeviceDiscoveryResults
                .SelectMany(ipDevice => ipDevice.GetTunnelingConnections())
                .Where(p => p.IndividualAddress.HasValue)
                .OrderBy(p => p.IndividualAddress);
            foreach (var parameters in allParameters)
            {
                var displayName = $"{parameters.Name} {parameters.HostAddress} ({parameters.IndividualAddress!.Value})";
                var allowed = config.Value.TunnelingAreaLineFilter.Any(p => parameters.IndividualAddress.Value.ToString().StartsWith($"{p}."));
                if (allowed)
                    allowedParameters.Add(parameters);
                logger.LogInformation("{ClassName} {Index}: {DisplayName} {Skipped}", nameof(KnxMonitorBgService), index++,
                    displayName, allowed ? string.Empty : " (Skipped)");
            }
            return allowedParameters;
        }
        throw new OperationCanceledException(cancellationToken);
    }

    private async Task<IpRoutingConnectorParameters> GetIpRoutingConnectorParameters(CancellationToken cancellationToken)
    {
        var attempt = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("{ClassName} start discovery of KNX IP routing devices...", nameof(KnxMonitorBgService));
            var ipDeviceDiscoveryResults = await KnxBus.DiscoverIpDevicesAsync(cancellationToken)
                .Where(p => p.Supports(config.Value.ServiceFamily, 1), cancellationToken)
                .ToArray(cancellationToken);
            var routingDevice = ipDeviceDiscoveryResults.FirstOrDefault();
            if (routingDevice is not null)
            {
                var parameters = new IpRoutingConnectorParameters(routingDevice.MulticastAddress)
                {
                    LocalIPAddress = routingDevice.LocalIPAddress
                };
                logger.LogInformation("{ClassName} routing device found: {FriendlyName} multicast={MulticastAddress}",
                    nameof(KnxMonitorBgService), routingDevice.FriendlyName, routingDevice.MulticastAddress);
                return parameters;
            }

            logger.Log(attempt % 10 == 0 ? LogLevel.Warning : LogLevel.Debug,
                "{ClassName} no KNX IP routing devices detected, attempt {Attempt}, retry in {RetryMs}ms...",
                nameof(KnxMonitorBgService), attempt, config.Value.DiscoveryRetryDelayMs);
            await Task.Delay(config.Value.DiscoveryRetryDelayMs, cancellationToken);
            attempt++;
        }
        throw new OperationCanceledException(cancellationToken);
    }

    private async Task<KnxBus?> TryConnectRouting(IpRoutingConnectorParameters parameters, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return null;
        var bus = new KnxBus(parameters);
        logger.LogInformation("{ClassName} connecting to routing multicast {MulticastAddress}...",
            nameof(KnxMonitorBgService), parameters.MulticastAddress);
        try
        {
            await bus.ConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning("{ClassName} routing connection to {MulticastAddress} failed, {Reason}",
                nameof(KnxMonitorBgService), parameters.MulticastAddress,
                ex.InnerException?.Message ?? ex.Message);
            await bus.DisposeAsync();
            return null;
        }

        if (bus.ConnectionState != BusConnectionState.Connected)
        {
            await bus.DisposeAsync();
            return null;
        }

        logger.LogInformation("{ClassName} routing connection to {MulticastAddress} succeeded!",
            nameof(KnxMonitorBgService), parameters.MulticastAddress);

        bus.InterfaceConfigurationChanged += Bus_InterfaceConfigurationChanged;
        bus.GroupMessageReceived += Bus_GroupMessageReceived;
        bus.ConnectionStateChanged += Bus_ConnectionStateChanged;

        return bus;
    }

    /// <summary>
    /// Fires when a message is received on the bus pertinent to a group address.
    /// </summary>
    private async void Bus_GroupMessageReceived(object? sender, GroupEventArgs e)
    {
        if (sender is null) return;
        if (e.Value is null) return;//occurs when we download to devices

        var bus = (KnxBus)sender;
        var busAddr = bus.InterfaceConfiguration.IndividualAddress;

        //tunneling-specific filters — routing receives all backbone traffic so these don't apply
        if (config.Value.ServiceFamily == ServiceFamily.Tunneling)
        {
            //only process telegrams whose source area/line matches this bus connection's area/line;
            //without this, routed telegrams are received by every connection and published multiple times
            if (e.SourceAddress.AreaAddress != busAddr.AreaAddress || e.SourceAddress.LineAddress != busAddr.LineAddress)
            {
                logger.LogDebug("{ClassName} dropped telegram [cross-line] from {SourceAddress} to {DestinationAddress} on {BusAddress}",
                    nameof(KnxMonitorBgService), e.SourceAddress, e.DestinationAddress, busAddr);
                return;
            }

            //drop telegrams from rival tunneling connections (e.g. another deployment sharing the same interfaces)
            var sourceAddress = e.SourceAddress.ToString();
            if (_allDiscoveredAddresses.Contains(sourceAddress) && !_connectedAddresses.ContainsKey(sourceAddress))
            {
                logger.LogDebug("{ClassName} dropped telegram [rival tunneling address] from {SourceAddress} to {DestinationAddress} on {BusAddress}",
                    nameof(KnxMonitorBgService), sourceAddress, e.DestinationAddress, busAddr);
                return;
            }
        }

        var groupAddress = e.DestinationAddress.ToString();//i.e. 1/2/3
        var kga = knxGroupAddressLookupSvc.GetKGAByAddress(groupAddress);
        if (kga is null) return;
        var tpl = e.Value.DecodeValue(kga, logger);
        if (tpl.ValueDecoded is not null)
        {
            var knxEvent = new KnxEvent(DateTime.UtcNow, e.ToKnxGroupEvent(), kga, e.Value, tpl.ValueDecoded, tpl.ValueLabelDecoded);
            await incomingBroker.PublishAsync(knxEvent);
        }
    }

    private void Bus_ConnectionStateChanged(object? sender, EventArgs e)
    {
        if (sender is null) return;
        var bus = (KnxBus)sender;
        logger.LogInformation("{ClassName} ConnectionStateChanged, ConnectionState={@ConnectionState}",
            nameof(KnxMonitorBgService), bus.ConnectionState);

        if (bus.ConnectionState is BusConnectionState.Connected)
            return;

        //find which area/line this bus belongs to and request reconnection
        var areaLine = KnxStatics.BusConnections
            .FirstOrDefault(kvp => ReferenceEquals(kvp.Value, bus)).Key;
        if (areaLine is not null)
        {
            logger.LogWarning("{ClassName} connection dropped for {AreaLine}, queuing reconnection",
                nameof(KnxMonitorBgService), areaLine);
            connectionNotifier.Notify(new KnxConnectionStateChange
            {
                AreaLine = areaLine,
                Connected = false,
                TimestampUtc = DateTime.UtcNow,
            });
            _reconnectChannel.Writer.TryWrite(areaLine);
        }
    }

    private void Bus_InterfaceConfigurationChanged(object? sender, EventArgs e)
        => logger.LogInformation("{ClassName} InterfaceConfigurationChanged={@EventArgs}", nameof(KnxMonitorBgService), e);
}
