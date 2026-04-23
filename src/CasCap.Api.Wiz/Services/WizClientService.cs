using System.Collections.Concurrent;

namespace CasCap.Services;

/// <summary>
/// Low-level UDP client for communicating with Wiz smart bulbs on the local network.
/// Handles discovery broadcasts and individual bulb commands via the Wiz JSON/UDP protocol.
/// </summary>
public class WizClientService(
    ILogger<WizClientService> logger,
    IOptions<WizConfig> config)
{
    const string DiscoveryPhoneMac = "AAAAAAAAAAAA";
    const string DiscoveryPhoneIp = "1.2.3.4";
    const string DiscoveryId = "1";

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly ConcurrentDictionary<string, WizBulb> _discoveredBulbs = new();
    private IPAddress? _resolvedBindAddress;
    private bool _bindAddressResolved;

    /// <summary>Returns a snapshot of all currently discovered bulbs.</summary>
    public IReadOnlyDictionary<string, WizBulb> DiscoveredBulbs => _discoveredBulbs;

    /// <summary>
    /// Broadcasts a registration message to discover Wiz bulbs on the local network.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of discovered bulbs keyed by IP address.</returns>
    public async Task<IReadOnlyDictionary<string, WizBulb>> DiscoverBulbsAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} starting bulb discovery broadcast", nameof(WizClientService));

        var cfg = config.Value;
        var payload = JsonSerializer.SerializeToUtf8Bytes(new { method = "registration", @params = new { phoneMac = DiscoveryPhoneMac, register = false, phoneIp = DiscoveryPhoneIp, id = DiscoveryId } }, s_jsonOptions);

        using var udpClient = CreateUdpClient(cfg);
        udpClient.EnableBroadcast = true;
        udpClient.Client.ReceiveTimeout = cfg.DiscoveryTimeoutMs;

        var broadcastEndpoint = new IPEndPoint(IPAddress.Parse(cfg.BroadcastAddress), cfg.BulbPort);
        await udpClient.SendAsync(payload, payload.Length, broadcastEndpoint);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(cfg.DiscoveryTimeoutMs);

        while (!cts.Token.IsCancellationRequested)
        {
            try
            {
                var result = await udpClient.ReceiveAsync(cts.Token);
                var json = Encoding.UTF8.GetString(result.Buffer);
                var response = JsonSerializer.Deserialize<WizResponse<WizPilotState>>(json, s_jsonOptions);
                var ip = result.RemoteEndPoint.Address.ToString();

                if (response?.Result is not null)
                {
                    var pilotState = await GetPilotAsync(ip, cancellationToken);
                    var systemConfig = await GetSystemConfigAsync(ip, cancellationToken);
                    var mac = response.Result.Mac ?? systemConfig?.Mac;
                    var deviceName = ResolveDeviceName(cfg, mac);
                    var bulb = new WizBulb
                    {
                        IpAddress = ip,
                        Mac = mac,
                        DeviceName = deviceName,
                        PilotState = pilotState ?? response.Result,
                        SystemConfig = systemConfig,
                        LastSeen = DateTime.UtcNow,
                    };
                    _discoveredBulbs.AddOrUpdate(ip, bulb, (_, _) => bulb);
                    logger.LogDebug("{ClassName} discovered bulb at {IpAddress} (MAC: {Mac})",
                        nameof(WizClientService), ip, bulb.Mac);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException)
            {
                break;
            }
        }

        logger.LogDebug("{ClassName} discovery complete, {BulbCount} bulb(s) found",
            nameof(WizClientService), _discoveredBulbs.Count);
        return _discoveredBulbs;
    }

    /// <summary>
    /// Gets the current pilot state of a single bulb.
    /// </summary>
    /// <param name="bulbIp">IP address of the target bulb.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<WizPilotState?> GetPilotAsync(string bulbIp, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} getPilot for {BulbIp}", nameof(WizClientService), bulbIp);
        var response = await SendCommandAsync<WizPilotState>(bulbIp, new { method = "getPilot" }, cancellationToken);
        return response?.Result;
    }

    /// <summary>
    /// Gets the system configuration of a single bulb.
    /// </summary>
    /// <param name="bulbIp">IP address of the target bulb.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<WizSystemConfig?> GetSystemConfigAsync(string bulbIp, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} getSystemConfig for {BulbIp}", nameof(WizClientService), bulbIp);
        var response = await SendCommandAsync<WizSystemConfig>(bulbIp, new { method = "getSystemConfig" }, cancellationToken);
        return response?.Result;
    }

    /// <summary>
    /// Sets the pilot state of a single bulb.
    /// </summary>
    /// <param name="bulbIp">IP address of the target bulb.</param>
    /// <param name="request">The desired state to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the command succeeded.</returns>
    public async Task<bool> SetPilotAsync(string bulbIp, WizSetPilotRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} setPilot for {BulbIp}", nameof(WizClientService), bulbIp);
        var response = await SendCommandAsync<JsonElement>(bulbIp, new { method = "setPilot", @params = request }, cancellationToken);
        return response?.Error is null;
    }

    private async Task<WizResponse<T>?> SendCommandAsync<T>(string bulbIp, object command, CancellationToken cancellationToken)
    {
        var cfg = config.Value;
        var payload = JsonSerializer.SerializeToUtf8Bytes(command, s_jsonOptions);

        using var udpClient = CreateUdpClient(cfg);
        var endpoint = new IPEndPoint(IPAddress.Parse(bulbIp), cfg.BulbPort);
        await udpClient.SendAsync(payload, payload.Length, endpoint);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(cfg.CommandTimeoutMs);

        try
        {
            var result = await udpClient.ReceiveAsync(cts.Token);
            var json = Encoding.UTF8.GetString(result.Buffer);
            return JsonSerializer.Deserialize<WizResponse<T>>(json, s_jsonOptions);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("{ClassName} command timed out for {BulbIp}", nameof(WizClientService), bulbIp);
            return null;
        }
        catch (SocketException ex)
        {
            logger.LogWarning(ex, "{ClassName} socket error communicating with {BulbIp}", nameof(WizClientService), bulbIp);
            return null;
        }
    }

    private UdpClient CreateUdpClient(WizConfig cfg)
    {
        var bindAddr = GetBindAddress(cfg);
        if (bindAddr is not null)
            return new UdpClient(new IPEndPoint(bindAddr, 0));
        return new UdpClient();
    }

    private IPAddress? GetBindAddress(WizConfig cfg)
    {
        if (!_bindAddressResolved)
        {
            _resolvedBindAddress = ResolveBindAddress(cfg);
            _bindAddressResolved = true;
        }
        return _resolvedBindAddress;
    }

    private IPAddress? ResolveBindAddress(WizConfig cfg)
    {
        if (string.IsNullOrEmpty(cfg.BindAddress))
            return null;

        if (!cfg.BindAddress.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return IPAddress.Parse(cfg.BindAddress);

        // Auto-detect: find a local interface whose network prefix matches BroadcastAddress.
        var bcastBytes = IPAddress.Parse(cfg.BroadcastAddress).GetAddressBytes();

        // Infer prefix length from broadcast — count leading non-255 octets.
        var prefixBytes = 0;
        for (var i = 0; i < 4; i++)
        {
            if (bcastBytes[i] == 255) break;
            prefixBytes++;
        }

        if (prefixBytes == 0)
        {
            logger.LogWarning("{ClassName} BindAddress=auto requires a subnet-directed BroadcastAddress, not 255.255.255.255",
                nameof(WizClientService));
            return null;
        }

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                var ipBytes = unicast.Address.GetAddressBytes();
                var match = true;
                for (var i = 0; i < prefixBytes; i++)
                {
                    if (ipBytes[i] != bcastBytes[i])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    logger.LogInformation("{ClassName} auto-detected BindAddress {BindAddress} on interface {InterfaceName}",
                        nameof(WizClientService), unicast.Address, nic.Name);
                    return unicast.Address;
                }
            }
        }

        logger.LogWarning("{ClassName} BindAddress=auto but no matching interface found for BroadcastAddress {BroadcastAddress}",
            nameof(WizClientService), cfg.BroadcastAddress);
        return null;
    }

    private static string? ResolveDeviceName(WizConfig cfg, string? mac)
    {
        if (string.IsNullOrEmpty(mac) || cfg.Devices.Length == 0)
            return null;
        return cfg.Devices.FirstOrDefault(d =>
            d.Mac.Equals(mac, StringComparison.OrdinalIgnoreCase))?.DeviceName;
    }

    /// <summary>
    /// Resolves a bulb identifier (device name, MAC, or IP) to an IP address
    /// using the currently discovered bulbs.
    /// </summary>
    /// <param name="bulbIdentifier">Device name, MAC address, or IP address.</param>
    /// <returns>The bulb IP address, or null if not found.</returns>
    public string? ResolveToIp(string bulbIdentifier)
    {
        // Direct IP match
        if (_discoveredBulbs.ContainsKey(bulbIdentifier))
            return bulbIdentifier;

        // Match by device name or MAC (case-insensitive)
        var bulb = _discoveredBulbs.Values.FirstOrDefault(b =>
            string.Equals(b.DeviceName, bulbIdentifier, StringComparison.OrdinalIgnoreCase)
            || string.Equals(b.Mac, bulbIdentifier, StringComparison.OrdinalIgnoreCase));
        return bulb?.IpAddress;
    }
}
