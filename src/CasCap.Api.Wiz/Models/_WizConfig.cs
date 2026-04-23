namespace CasCap.Models;

/// <summary>Configuration options for the Wiz smart lighting integration.</summary>
public record WizConfig : IAppConfig, IHealthCheckConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(WizConfig)}";

    /// <summary>UDP port used to send commands to Wiz bulbs.</summary>
    /// <remarks>Defaults to 38899.</remarks>
    [Required, Range(1, 65535)]
    public required int BulbPort { get; init; } = 38899;

    /// <summary>
    /// Timeout in milliseconds for UDP discovery broadcasts.
    /// Used by <see cref="CasCap.Services.WizClientService"/>.
    /// </summary>
    /// <remarks>Defaults to 3000.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int DiscoveryTimeoutMs { get; init; } = 3_000;

    /// <summary>
    /// Timeout in milliseconds for individual bulb command responses.
    /// Used by <see cref="CasCap.Services.WizClientService"/>.
    /// </summary>
    /// <remarks>Defaults to 2000.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int CommandTimeoutMs { get; init; } = 2_000;

    /// <summary>
    /// Delay in milliseconds between background discovery polling cycles.
    /// Used by <see cref="CasCap.Services.WizDiscoveryBgService"/>.
    /// </summary>
    /// <remarks>Defaults to 10000.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int DiscoveryPollingDelayMs { get; init; } = 10_000;

    /// <summary>
    /// Local IP address to bind the UDP socket to for discovery and commands.
    /// Used by <see cref="CasCap.Services.WizClientService"/>.
    /// </summary>
    /// <remarks>
    /// Defaults to null (bind to all interfaces). Set to the Multus macvlan interface IP
    /// in Kubernetes so UDP broadcast goes out the correct network interface.
    /// </remarks>
    public string? BindAddress { get; init; }

    /// <summary>The broadcast address used for bulb discovery.</summary>
    /// <remarks>Defaults to 255.255.255.255. Use subnet-directed broadcast (e.g. 192.168.1.255) in Kubernetes.</remarks>
    [Required, MinLength(1)]
    public required string BroadcastAddress { get; init; } = "255.255.255.255";

    /// <summary>
    /// Known Wiz bulbs with human-readable names. Optional — discovered bulbs not in this
    /// list are still tracked but use their MAC or IP as the device name.
    /// </summary>
    public WizDevice[] Devices { get; init; } = [];

    /// <summary>
    /// Required by <see cref="IHealthCheckConfig"/>. Not used by <see cref="CasCap.HealthChecks.WizConnectionHealthCheck"/>
    /// which checks discovered bulb count instead.
    /// </summary>
    [Required, MinLength(1)]
    public required string HealthCheckUri { get; init; } = "255.255.255.255";

    /// <inheritdoc cref="KubernetesProbeTypes"/>
    [Required]
    public required KubernetesProbeTypes HealthCheck { get; init; } = KubernetesProbeTypes.Readiness;

    /// <summary>Azure Table Storage connection string for Wiz sinks.</summary>
    public string? AzureTableStorageConnectionString { get; init; }

    /// <summary>Number of days to retain time-series data in Redis sorted sets.</summary>
    /// <remarks>Defaults to 7.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int RedisSeriesExpiryDays { get; init; } = 7;

    /// <summary>Sink configuration for event persistence.</summary>
    [Required, ValidateObjectMembers]
    public required SinkConfig Sinks { get; init; } = default!;
}
