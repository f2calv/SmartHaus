namespace CasCap.Models;

/// <summary>
/// Configuration options for the Buderus KM200 heating system integration.
/// </summary>
public record BuderusConfig : IAppConfig, IHealthCheckConfig, IAzTableStorageConfig, IMetricsConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(BuderusConfig)}";

    /// <summary>
    /// The base address of the Buderus KM200 gateway (e.g. "http://192.168.1.248").
    /// </summary>
    [Required, Url]
    public required string BaseAddress { get; init; }

    /// <summary>The health check endpoint path on the Buderus KM200 gateway.</summary>
    /// <remarks>Defaults to <c>"system"</c>.</remarks>
    [Required]
    public required string HealthCheckUri { get; init; } = "system";

    /// <inheritdoc cref="KubernetesProbeTypes"/>
    [Required]
    public required KubernetesProbeTypes HealthCheck { get; init; } = KubernetesProbeTypes.Readiness;

    /// <summary>The port of the Buderus KM200 gateway.</summary>
    /// <remarks>Defaults to <c>80</c>.</remarks>
    [Required, Range(1, 65535)]
    public required int Port { get; init; } = 80;

    /// <summary>
    /// The gateway password for authentication with the Buderus KM200 device.
    /// </summary>
    [Required, MinLength(1)]
    public required string GatewayPassword { get; init; }

    /// <summary>
    /// The private password for authentication with the Buderus KM200 device.
    /// </summary>
    [Required, MinLength(1)]
    public required string PrivatePassword { get; init; }

    /// <inheritdoc/>
    [Required]
    public required string AzureTableStorageConnectionString { get; init; }

    /// <inheritdoc/>
    /// <remarks>Defaults to <see cref="KubernetesProbeTypes.Startup"/>.</remarks>
    [Required]
    public required KubernetesProbeTypes HealthCheckAzureTableStorage { get; init; } = KubernetesProbeTypes.None;

    /// <summary>
    /// Configuration for event data sinks.
    /// </summary>
    [Required, ValidateObjectMembers]
    public required SinkConfig Sinks { get; init; }

    /// <summary>Expiry in days applied to Redis series sorted set keys.</summary>
    /// <remarks>Defaults to <c>7</c>.</remarks>
    [Range(1, int.MaxValue)]
    public int RedisSeriesExpiryDays { get; init; } = 7;

    /// <summary>
    /// Polling interval in milliseconds for the monitor background service.
    /// Used by <see cref="CasCap.Services.BuderusKm200MonitorBgService"/>.
    /// </summary>
    /// <remarks>Defaults to <c>60000</c> (1 minute).</remarks>
    [Range(1, int.MaxValue)]
    public int PollingIntervalMs { get; init; } = 60_000;

    /// <summary>
    /// Delay in milliseconds between individual datapoint queries within a single polling cycle.
    /// Used by <see cref="CasCap.Services.BuderusKm200MonitorBgService"/>.
    /// </summary>
    /// <remarks>Defaults to <c>500</c>.</remarks>
    [Range(1, int.MaxValue)]
    public int DatapointDelayMs { get; init; } = 500;

    /// <summary>
    /// Delay in milliseconds between each poll when waiting for the KM200 connection to
    /// become active at startup. Used by <see cref="CasCap.Services.BuderusKm200MonitorBgService"/>.
    /// </summary>
    /// <remarks>Defaults to <c>1000</c> ms.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int ConnectionPollingDelayMs { get; init; } = 1_000;

    /// <summary>
    /// Number of connection polling attempts between each escalation from
    /// <see cref="Microsoft.Extensions.Logging.LogLevel.Trace"/> to
    /// <see cref="Microsoft.Extensions.Logging.LogLevel.Warning"/>. Used by
    /// <see cref="CasCap.Services.BuderusKm200MonitorBgService"/>.
    /// </summary>
    /// <remarks>Defaults to <c>10</c>.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int ConnectionLogEscalationInterval { get; init; } = 10;

    /// <summary>
    /// Maps KM200 datapoint IDs to Azure Table Storage column names and optional OpenTelemetry metric definitions.
    /// Keys are polled by the monitor service; <see cref="DatapointMapping.ColumnName"/> values become columns in the snapshot table.
    /// </summary>
    [MinLength(1)]
    public Dictionary<string, DatapointMapping> DatapointMappings { get; init; } = [];

    /// <inheritdoc/>
    /// <remarks>Defaults to <c>"haus"</c>.</remarks>
    [Required]
    public required string MetricNamePrefix { get; init; } = "haus";

    /// <inheritdoc/>
    /// <remarks>Defaults to <c>"CasCap.App"</c>.</remarks>
    [Required]
    public required string OtelServiceName { get; init; } = "CasCap.App";
}
