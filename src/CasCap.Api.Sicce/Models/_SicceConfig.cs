namespace CasCap.Models;

/// <summary>Sicce aquarium pump API configuration.</summary>
public record SicceConfig : IAppConfig, IHealthCheckConfig, IAzTableStorageConfig, IMetricsConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(SicceConfig)}";

    /// <summary>
    /// The base address of the Sicce API. Defaults to <c>"https://sicce.thingscloud.it"</c>.
    /// </summary>
    [Required, Url]
    public required string BaseAddress { get; init; } = "https://sicce.thingscloud.it";

    /// <summary>
    /// The health check endpoint path on the Sicce API. Defaults to <c>"/api/v3"</c>.
    /// </summary>
    [Required]
    public required string HealthCheckUri { get; init; } = "/api/v3";

    /// <inheritdoc cref="KubernetesProbeTypes"/>
    [Required]
    public required KubernetesProbeTypes HealthCheck { get; init; } = KubernetesProbeTypes.Readiness;

    /// <summary>Device authentication token.</summary>
    [Required, MinLength(1)]
    public required string DeviceToken { get; init; }

    /// <summary>Vendor API key.</summary>
    [Required, MinLength(1)]
    public required string VendorKey { get; init; }

    /// <summary>
    /// Polling interval in milliseconds for the monitor background service.
    /// Used by <see cref="CasCap.Services.SicceBgService"/>.
    /// </summary>
    /// <remarks>Defaults to <c>30000</c> (30 seconds).</remarks>
    [Range(1, int.MaxValue)]
    public int PollingIntervalMs { get; init; } = 30_000;

    /// <summary>
    /// Delay in milliseconds between each poll when waiting for the Sicce connection to
    /// become active at startup. Used by <see cref="CasCap.Services.SicceBgService"/>.
    /// </summary>
    /// <remarks>Defaults to <c>5000</c> ms.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int ConnectionPollingDelayMs { get; init; } = 5_000;

    /// <summary>
    /// Number of connection polling attempts between each escalation from
    /// <see cref="Microsoft.Extensions.Logging.LogLevel.Trace"/> to
    /// <see cref="Microsoft.Extensions.Logging.LogLevel.Warning"/>. Used by
    /// <see cref="CasCap.Services.SicceBgService"/>.
    /// </summary>
    /// <remarks>Defaults to <c>10</c>.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int ConnectionLogEscalationInterval { get; init; } = 10;

    /// <inheritdoc/>
    [Required]
    public required string AzureTableStorageConnectionString { get; init; }

    /// <inheritdoc/>
    /// <remarks>Defaults to <see cref="KubernetesProbeTypes.None"/>.</remarks>
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

    /// <inheritdoc/>
    /// <remarks>Defaults to <c>"haus"</c>.</remarks>
    [Required]
    public required string MetricNamePrefix { get; init; } = "haus";

    /// <inheritdoc/>
    /// <remarks>Defaults to <c>"CasCap.App"</c>.</remarks>
    [Required]
    public required string OtelServiceName { get; init; } = "CasCap.App";
}
