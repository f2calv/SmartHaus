using Knx.Falcon;
using Knx.Falcon.Configuration;
using Knx.Falcon.KnxnetIp;
using Knx.Falcon.Sdk;

namespace CasCap.Services;

/// <summary>
/// Owns bus connections, reconnection requests and incoming telegrams for one leadership run.
/// </summary>
public sealed partial class KnxMonitorService(
    ILogger<KnxMonitorService> logger,
    IOptions<KnxConfig> config,
    TimeProvider timeProvider,
    KnxGroupAddressLookupService knxGroupAddressLookupSvc,
    KnxWorkerLifetime workerLifetime,
    IKnxTelegramBroker<KnxEvent> incomingBroker,
    KnxConnectionNotifier connectionNotifier) : IKnxMonitor
{
    private KnxConnectionHealthCheck ConnectionHealth => workerLifetime.Health;
    private readonly object _callbackGate = new();
    private readonly ConcurrentDictionary<KnxBus, byte> _ownedBuses = new();
    // TODO: Bound callback ingress once measured bursts and an explicit overflow/recovery policy
    // allow it. Falcon's synchronous events cannot await backpressure; silent drops are unsafe.
    private readonly Channel<KnxEvent> _telegrams = Channel.CreateUnbounded<KnxEvent>(
        new UnboundedChannelOptions { SingleReader = true });
    private CancellationToken _ownershipToken;
    private CancellationTokenRegistration _ownershipRegistration;
    private IpRoutingConnectorParameters? _routingParameters;
    private volatile bool _stopping;
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
    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        _ownershipToken = cancellationToken;
        ConnectionHealth.IsLeader = true;
        _ownershipRegistration = cancellationToken.Register(WithdrawOwnership);
        cancellationToken.ThrowIfCancellationRequested();
        switch (config.Value.ServiceFamily)
        {
            case ServiceFamily.Tunneling:
                await ConnectTunnelingAsync(cancellationToken).ConfigureAwait(false);
                break;
            case ServiceFamily.Routing:
                await ConnectRoutingAsync(cancellationToken).ConfigureAwait(false);
                break;
            default:
                throw new NotSupportedException(
                    $"{nameof(KnxConfig.ServiceFamily)} '{config.Value.ServiceFamily}' is not supported. " +
                    $"Use {nameof(ServiceFamily.Tunneling)} or {nameof(ServiceFamily.Routing)}.");
        }
    }

    /// <inheritdoc/>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(WithdrawOwnership);
        Func<CancellationToken, Task>[] workers =
            [MonitorHealthAsync, ProcessReconnectionRequestsAsync, PublishTelegramsAsync];
        try
        {
            await workerLifetime.RunAsync(workers, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            WithdrawOwnership();
        }
    }

    private void WithdrawOwnership()
    {
        lock (_callbackGate)
        {
            _stopping = true;
            ConnectionHealth.ConnectionActive = false;
            ConnectionHealth.IsLeader = false;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        WithdrawOwnership();
        await _ownershipRegistration.DisposeAsync().ConfigureAwait(false);
        _telegrams.Writer.TryComplete();
        _reconnectChannel.Writer.TryComplete();
        KnxStatics.BusConnections.Clear();
        List<Exception>? failures = null;
        foreach (var bus in _ownedBuses.Keys)
            try
            {
                await DisconnectBusAsync(bus).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                (failures ??= []).Add(ex);
            }
        _lineParameters.Clear();
        _allDiscoveredAddresses.Clear();
        _connectedAddresses.Clear();
        if (failures is not null)
            throw new AggregateException("KNX connection cleanup failed.", failures);
    }

    private async Task PublishTelegramsAsync(CancellationToken cancellationToken)
    {
        await foreach (var telegram in _telegrams.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await incomingBroker.PublishAsync(telegram, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Buffers a decoded telegram only while this ownership run accepts callbacks.</summary>
    internal bool TryQueueTelegram(KnxEvent telegram)
    {
        lock (_callbackGate)
            return !_stopping && !_ownershipToken.IsCancellationRequested && _telegrams.Writer.TryWrite(telegram);
    }

    private async Task ConnectTunnelingAsync(CancellationToken cancellationToken)
    {
        //gather information on possible Areas+Lines
        var ipTunnelingConnectorParametersAll = await GetIpTunnelingConnectorParameters(cancellationToken).ConfigureAwait(false);

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
            if (!await ConnectLine(areaLine, cancellationToken).ConfigureAwait(false))
                _reconnectChannel.Writer.TryWrite(areaLine);
    }

    private async Task ConnectRoutingAsync(CancellationToken cancellationToken)
    {
        _routingParameters = await GetIpRoutingConnectorParameters(cancellationToken).ConfigureAwait(false);

        //routing uses a single multicast connection covering the entire KNX backbone
        var routingAreaLine = new KnxAreaLine { Area = 0, Line = 0 };
        KnxStatics.BusConnections.Clear();
        KnxStatics.BusConnections[routingAreaLine] = null;

        var bus = await TryConnectRouting(_routingParameters, cancellationToken).ConfigureAwait(false);
        if (bus is not null)
            KnxStatics.BusConnections[routingAreaLine] = bus;

        else
            _reconnectChannel.Writer.TryWrite(routingAreaLine);
    }

    /// <summary>
    /// Periodically polls all bus connections and updates the <see cref="KnxConnectionHealthCheck"/>.
    /// </summary>
    private async Task MonitorHealthAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var allConnected = !KnxStatics.BusConnections.IsEmpty && KnxStatics.BusConnections.Values
                .All(bus => bus?.ConnectionState == BusConnectionState.Connected);
            lock (_callbackGate)
                ConnectionHealth.ConnectionActive = !_stopping && allConnected;
            await Task.Delay(TimeSpan.FromMilliseconds(config.Value.ConnectionHealthPollingDelayMs), timeProvider, cancellationToken).ConfigureAwait(false);
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
            LogReconnectionRequested(logger, nameof(KnxMonitorService), areaLine);

            var backoffMs = config.Value.ReconnectBackoffMs;
            var maxBackoffMs = config.Value.ReconnectMaxBackoffMs;
            var connected = false;
            while (!connected && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(backoffMs), timeProvider, cancellationToken).ConfigureAwait(false);
                connected = config.Value.ServiceFamily == ServiceFamily.Routing
                    ? await ReconnectRoutingAsync(areaLine, cancellationToken).ConfigureAwait(false)
                    : await ConnectLine(areaLine, cancellationToken).ConfigureAwait(false);
                if (!connected)
                {
                    LogReconnectionFailed(logger, nameof(KnxMonitorService), areaLine, backoffMs);
                    backoffMs = Math.Min(backoffMs * 2, maxBackoffMs);
                }
            }

            if (connected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LogReconnectionSucceeded(logger, nameof(KnxMonitorService), areaLine);
                connectionNotifier.Notify(new KnxConnectionStateChange
                {
                    AreaLine = areaLine,
                    Connected = true,
                    TimestampUtc = timeProvider.GetUtcNow().UtcDateTime,
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
        cancellationToken.ThrowIfCancellationRequested();
        if (!_lineParameters.TryGetValue(areaLine, out var parametersList))
        {
            LogNoStoredParameters(logger, nameof(KnxMonitorService), areaLine);
            return false;
        }

        LogConnectingToAreaLine(logger, nameof(KnxMonitorService), areaLine);

        //dispose the old bus connection if one exists
        if (KnxStatics.BusConnections.TryGetValue(areaLine, out var oldBus) && oldBus is not null)
        {
            _connectedAddresses.TryRemove(oldBus.InterfaceConfiguration.IndividualAddress.ToString(), out _);
            await DisconnectBusAsync(oldBus).ConfigureAwait(false);
            KnxStatics.BusConnections[areaLine] = null;
        }

        //iterate over the individual addresses and attempt a connection
        foreach (var parameters in parametersList)
        {
            var bus = await TryConnect(parameters, cancellationToken).ConfigureAwait(false);
            if (bus is not null)
            {
                KnxStatics.BusConnections[areaLine] = bus;
                _connectedAddresses[parameters.IndividualAddress!.Value.ToString()] = 0;
                return true;
            }
        }

        return false;
    }

    private async Task<bool> ReconnectRoutingAsync(KnxAreaLine areaLine, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (KnxStatics.BusConnections.TryGetValue(areaLine, out var oldBus) && oldBus is not null)
        {
            await DisconnectBusAsync(oldBus).ConfigureAwait(false);
            KnxStatics.BusConnections[areaLine] = null;
        }
        var bus = await TryConnectRouting(_routingParameters!, cancellationToken).ConfigureAwait(false);
        KnxStatics.BusConnections[areaLine] = bus;
        return bus is not null;
    }

    /// <summary>
    /// Attempts a single connection to the given tunneling connector parameters.
    /// </summary>
    private async Task<KnxBus?> TryConnect(IpTunnelingConnectorParameters parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bus = new KnxBus(parameters);
        logger.LogInformation("{ClassName} connecting to {Name} {HostAddress} ({IndividualAddress})...",
            nameof(KnxMonitorService), parameters.Name, parameters.HostAddress, parameters.IndividualAddress);
        try
        {
            await bus.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await bus.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{ClassName} tunneling connection failed", nameof(KnxMonitorService));
            await bus.DisposeAsync().ConfigureAwait(false);
            return null;
        }

        if (cancellationToken.IsCancellationRequested || bus.ConnectionState != BusConnectionState.Connected)
        {
            await bus.DisposeAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }

        logger.LogInformation("{ClassName} connection to {Name} {HostAddress} ({IndividualAddress}) succeeded!",
            nameof(KnxMonitorService), parameters.Name, parameters.HostAddress, parameters.IndividualAddress);

        _ownedBuses.TryAdd(bus, 0);
        //wire-up event handlers
        bus.InterfaceConfigurationChanged += Bus_InterfaceConfigurationChanged;
        bus.GroupMessageReceived += Bus_GroupMessageReceived;
        bus.ConnectionStateChanged += Bus_ConnectionStateChanged;

        LogInterfaceFeatures(logger, nameof(KnxMonitorService), bus.InterfaceFeatures?.ToString() ?? string.Empty);
        LogInterfaceConfiguration(logger, nameof(KnxMonitorService), bus.InterfaceConfiguration?.ToString() ?? string.Empty);

        return bus;
    }

    /// <summary>
    /// Unwires event handlers and disposes a <see cref="KnxBus"/> instance.
    /// </summary>
    private async ValueTask DisconnectBusAsync(KnxBus bus)
    {
        _ownedBuses.TryRemove(bus, out _);
        bus.InterfaceConfigurationChanged -= Bus_InterfaceConfigurationChanged;
        bus.GroupMessageReceived -= Bus_GroupMessageReceived;
        bus.ConnectionStateChanged -= Bus_ConnectionStateChanged;
        await bus.DisposeAsync().ConfigureAwait(false);
    }

    private async Task<List<IpTunnelingConnectorParameters>> GetIpTunnelingConnectorParameters(CancellationToken cancellationToken)
    {
        var attempt = 1;
        while (!cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug("{ClassName} start discovery of KNX IP devices...", nameof(KnxMonitorService));
            var ipDeviceDiscoveryResults = await KnxBus.DiscoverIpDevicesAsync(cancellationToken)
                .Where(p => p.Supports(config.Value.ServiceFamily, 1), cancellationToken)
                .ToArray(cancellationToken).ConfigureAwait(false);

            if (ipDeviceDiscoveryResults.Length == 0)
            {
                logger.Log(attempt % 10 == 0 ? LogLevel.Warning : LogLevel.Debug,
                    "{ClassName} no KNX IP devices detected, attempt '{Attempt}', retry in {RetryMs}ms...",
                    nameof(KnxMonitorService), attempt, config.Value.DiscoveryRetryDelayMs);
                await Task.Delay(TimeSpan.FromMilliseconds(config.Value.DiscoveryRetryDelayMs), timeProvider, cancellationToken).ConfigureAwait(false);
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
                logger.LogInformation("{ClassName} {Index}: {DisplayName} {Skipped}", nameof(KnxMonitorService), index++,
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
            logger.LogDebug("{ClassName} start discovery of KNX IP routing devices...", nameof(KnxMonitorService));
            var ipDeviceDiscoveryResults = await KnxBus.DiscoverIpDevicesAsync(cancellationToken)
                .Where(p => p.Supports(config.Value.ServiceFamily, 1), cancellationToken)
                .ToArray(cancellationToken).ConfigureAwait(false);
            var routingDevice = ipDeviceDiscoveryResults.FirstOrDefault();
            if (routingDevice is not null)
            {
                var parameters = new IpRoutingConnectorParameters(routingDevice.MulticastAddress)
                {
                    LocalIPAddress = routingDevice.LocalIPAddress
                };
                logger.LogInformation("{ClassName} routing device found: {FriendlyName} multicast={MulticastAddress}",
                    nameof(KnxMonitorService), routingDevice.FriendlyName, routingDevice.MulticastAddress);
                return parameters;
            }

            logger.Log(attempt % 10 == 0 ? LogLevel.Warning : LogLevel.Debug,
                "{ClassName} no KNX IP routing devices detected, attempt {Attempt}, retry in {RetryMs}ms...",
                nameof(KnxMonitorService), attempt, config.Value.DiscoveryRetryDelayMs);
            await Task.Delay(TimeSpan.FromMilliseconds(config.Value.DiscoveryRetryDelayMs), timeProvider, cancellationToken).ConfigureAwait(false);
            attempt++;
        }
        throw new OperationCanceledException(cancellationToken);
    }

    private async Task<KnxBus?> TryConnectRouting(IpRoutingConnectorParameters parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bus = new KnxBus(parameters);
        logger.LogInformation("{ClassName} connecting to routing multicast {MulticastAddress}...",
            nameof(KnxMonitorService), parameters.MulticastAddress);
        try
        {
            await bus.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await bus.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{ClassName} routing connection failed", nameof(KnxMonitorService));
            await bus.DisposeAsync().ConfigureAwait(false);
            return null;
        }

        if (cancellationToken.IsCancellationRequested || bus.ConnectionState != BusConnectionState.Connected)
        {
            await bus.DisposeAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }

        logger.LogInformation("{ClassName} routing connection to {MulticastAddress} succeeded!",
            nameof(KnxMonitorService), parameters.MulticastAddress);

        _ownedBuses.TryAdd(bus, 0);
        bus.InterfaceConfigurationChanged += Bus_InterfaceConfigurationChanged;
        bus.GroupMessageReceived += Bus_GroupMessageReceived;
        bus.ConnectionStateChanged += Bus_ConnectionStateChanged;

        return bus;
    }

    /// <summary>
    /// Fires when a message is received on the bus pertinent to a group address.
    /// </summary>
    private void Bus_GroupMessageReceived(object? sender, GroupEventArgs e)
    {
        lock (_callbackGate)
        {
            if (_stopping || _ownershipToken.IsCancellationRequested || sender is not KnxBus bus || !_ownedBuses.ContainsKey(bus))
                return;
            try
            {
                QueueTelegram(bus, e);
            }
            catch (Exception ex)
            {
                // Falcon callbacks cannot be awaited; surface decoding faults through the owned reader.
                _telegrams.Writer.TryComplete(ex);
            }
        }
    }

    private void QueueTelegram(KnxBus bus, GroupEventArgs e)
    {
        if (e.Value is null) return;//occurs when we download to devices

        var busAddr = bus.InterfaceConfiguration.IndividualAddress;

        //tunneling-specific filters — routing receives all backbone traffic so these don't apply
        if (config.Value.ServiceFamily == ServiceFamily.Tunneling)
        {
            //only process telegrams whose source area/line matches this bus connection's area/line;
            //without this, routed telegrams are received by every connection and published multiple times
            if (e.SourceAddress.AreaAddress != busAddr.AreaAddress || e.SourceAddress.LineAddress != busAddr.LineAddress)
            {
                LogDroppedCrossLine(logger, nameof(KnxMonitorService), e.SourceAddress, e.DestinationAddress, busAddr);
                return;
            }

            //drop telegrams from rival tunneling connections (e.g. another deployment sharing the same interfaces)
            var sourceAddress = e.SourceAddress.ToString();
            if (_allDiscoveredAddresses.Contains(sourceAddress) && !_connectedAddresses.ContainsKey(sourceAddress))
            {
                LogDroppedRivalAddress(logger, nameof(KnxMonitorService), sourceAddress, e.DestinationAddress, busAddr);
                return;
            }
        }

        var groupAddress = e.DestinationAddress.ToString();//i.e. 1/2/3
        var kga = knxGroupAddressLookupSvc.GetKGAByAddress(groupAddress);
        if (kga is null) return;
        var tpl = e.Value.DecodeValue(kga, logger);
        if (tpl.ValueDecoded is not null)
        {
            var knxEvent = new KnxEvent(timeProvider.GetUtcNow().UtcDateTime, e.ToKnxGroupEvent(), kga, e.Value, tpl.ValueDecoded, tpl.ValueLabelDecoded);
            TryQueueTelegram(knxEvent);
        }
    }

    private void Bus_ConnectionStateChanged(object? sender, EventArgs e)
    {
        lock (_callbackGate)
            HandleConnectionStateChanged(sender);
    }

    private void HandleConnectionStateChanged(object? sender)
    {
        if (_stopping || _ownershipToken.IsCancellationRequested || sender is not KnxBus bus || !_ownedBuses.ContainsKey(bus))
            return;
        LogConnectionStateChanged(logger, nameof(KnxMonitorService), bus.ConnectionState.ToString());

        if (bus.ConnectionState is BusConnectionState.Connected)
            return;

        //find which area/line this bus belongs to and request reconnection
        var areaLine = KnxStatics.BusConnections
            .FirstOrDefault(kvp => ReferenceEquals(kvp.Value, bus)).Key;
        if (areaLine is not null)
        {
            ConnectionHealth.ConnectionActive = false;
            LogConnectionDropped(logger, nameof(KnxMonitorService), areaLine);
            connectionNotifier.Notify(new KnxConnectionStateChange
            {
                AreaLine = areaLine,
                Connected = false,
                TimestampUtc = timeProvider.GetUtcNow().UtcDateTime,
            });
            _reconnectChannel.Writer.TryWrite(areaLine);
        }
    }

    private void Bus_InterfaceConfigurationChanged(object? sender, EventArgs e)
        => LogInterfaceConfigurationChanged(logger, nameof(KnxMonitorService));

    [LoggerMessage(Level = LogLevel.Trace, Message = "{ClassName} InterfaceFeatures {InterfaceFeatures}")]
    private static partial void LogInterfaceFeatures(ILogger logger, string className, string interfaceFeatures);

    [LoggerMessage(Level = LogLevel.Trace, Message = "{ClassName} InterfaceConfiguration {InterfaceConfiguration}")]
    private static partial void LogInterfaceConfiguration(ILogger logger, string className, string interfaceConfiguration);

    [LoggerMessage(Level = LogLevel.Information, Message = "{ClassName} ConnectionStateChanged, ConnectionState={ConnectionState}")]
    private static partial void LogConnectionStateChanged(ILogger logger, string className, string connectionState);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ClassName} dropped telegram [cross-line] from {SourceAddress} to {DestinationAddress} on {BusAddress}")]
    private static partial void LogDroppedCrossLine(ILogger logger, string className, IndividualAddress sourceAddress, GroupAddress destinationAddress, IndividualAddress busAddress);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ClassName} dropped telegram [rival tunneling address] from {SourceAddress} to {DestinationAddress} on {BusAddress}")]
    private static partial void LogDroppedRivalAddress(ILogger logger, string className, string sourceAddress, GroupAddress destinationAddress, IndividualAddress busAddress);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{ClassName} reconnection requested for {AreaLine}")]
    private static partial void LogReconnectionRequested(ILogger logger, string className, KnxAreaLine areaLine);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{ClassName} reconnection to {AreaLine} failed, retrying in {BackoffMs}ms")]
    private static partial void LogReconnectionFailed(ILogger logger, string className, KnxAreaLine areaLine, int backoffMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "{ClassName} reconnection to {AreaLine} succeeded")]
    private static partial void LogReconnectionSucceeded(ILogger logger, string className, KnxAreaLine areaLine);

    [LoggerMessage(Level = LogLevel.Error, Message = "{ClassName} no stored discovery parameters for {AreaLine}")]
    private static partial void LogNoStoredParameters(ILogger logger, string className, KnxAreaLine areaLine);

    [LoggerMessage(Level = LogLevel.Information, Message = "{ClassName} connecting to {AreaLine}")]
    private static partial void LogConnectingToAreaLine(ILogger logger, string className, KnxAreaLine areaLine);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{ClassName} connection dropped for {AreaLine}, queuing reconnection")]
    private static partial void LogConnectionDropped(ILogger logger, string className, KnxAreaLine areaLine);

    [LoggerMessage(Level = LogLevel.Information, Message = "{ClassName} InterfaceConfigurationChanged")]
    private static partial void LogInterfaceConfigurationChanged(ILogger logger, string className);
}
