namespace CasCap.Models;

/// <summary>
/// Configuration for the Fronius Symo solar inverter integration.
/// </summary>
public record FroniusConfig : IAppConfig, IHealthCheckConfig, IAzTableStorageConfig, IMetricsConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(FroniusConfig)}";

    /// <summary>Whether JSON debug output is enabled.</summary>
    /// <remarks>Defaults to <see langword="false"/>.</remarks>
    [Required]
    public required bool JsonDebugEnabled { get; init; } = false;

    /// <summary>
    /// Path for JSON debug output files.
    /// </summary>
    public string? JsonDebugPath { get; init; }

    /// <summary>
    /// The base address of the Fronius Symo inverter (e.g. "http://192.168.1.248").
    /// </summary>
    [Required, Url]
    public required string BaseAddress { get; init; }

    /// <summary>The health check endpoint path on the Fronius Symo inverter.</summary>
    /// <remarks>Defaults to <c>"solar_api/v1/GetPowerFlowRealtimeData.fcgi"</c>.</remarks>
    [Required]
    public required string HealthCheckUri { get; init; } = "solar_api/v1/GetPowerFlowRealtimeData.fcgi";

    /// <inheritdoc cref="KubernetesProbeTypes"/>
    [Required]
    public required KubernetesProbeTypes HealthCheck { get; init; } = KubernetesProbeTypes.Readiness;

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
    /// Used by <see cref="CasCap.Services.FroniusMonitorBgService"/>.
    /// </summary>
    /// <remarks>Defaults to <c>1000</c> (1 second).</remarks>
    [Range(1, int.MaxValue)]
    public int PollingIntervalMs { get; init; } = 1_000;

    /// <summary>
    /// Delay in milliseconds between each poll when waiting for the Fronius connection to
    /// become active at startup. Used by <see cref="CasCap.Services.FroniusMonitorBgService"/>.
    /// </summary>
    /// <remarks>Defaults to <c>1000</c> ms.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int ConnectionPollingDelayMs { get; init; } = 1_000;

    /// <summary>
    /// Number of connection polling attempts between each escalation from
    /// <see cref="Microsoft.Extensions.Logging.LogLevel.Trace"/> to
    /// <see cref="Microsoft.Extensions.Logging.LogLevel.Warning"/>. Used by
    /// <see cref="CasCap.Services.FroniusMonitorBgService"/>.
    /// </summary>
    /// <remarks>Defaults to <c>10</c>.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int ConnectionLogEscalationInterval { get; init; } = 10;

    /// <summary>
    /// Battery state-of-charge threshold (0.0–1.0) above which the comms stream sink
    /// writes an alert event.
    /// Used by <see cref="CasCap.Services.FroniusSinkCommsStreamService"/>.
    /// </summary>
    /// <remarks>Defaults to <c>0.95</c> (95%).</remarks>
    [Range(0.0, 1.0)]
    public double SocAlertThreshold { get; init; } = 0.95;

    /// <summary>
    /// Hysteresis band (0.0–1.0) applied below <see cref="SocAlertThreshold"/> before the
    /// alert can re-arm. Once the alert fires, SOC must drop below
    /// <c>SocAlertThreshold − SocAlertHysteresis</c> before a new alert is possible.
    /// Used by <see cref="CasCap.Services.FroniusSinkCommsStreamService"/>.
    /// </summary>
    /// <remarks>Defaults to <c>0.05</c> (5%).</remarks>
    [Range(0.0, 1.0)]
    public double SocAlertHysteresis { get; init; } = 0.05;

    /// <summary>
    /// Minimum cooldown in milliseconds between successive SOC alert events.
    /// Even if the hysteresis condition is met, no new alert fires until this
    /// interval has elapsed since the last alert.
    /// Used by <see cref="CasCap.Services.FroniusSinkCommsStreamService"/>.
    /// </summary>
    /// <remarks>Defaults to <c>300000</c> (5 minutes).</remarks>
    [Range(1, int.MaxValue)]
    public int SocAlertCooldownMs { get; init; } = 300_000;

    /// <inheritdoc/>
    /// <remarks>Defaults to <c>"haus"</c>.</remarks>
    [Required]
    public required string MetricNamePrefix { get; init; } = "haus";

    /// <inheritdoc/>
    /// <remarks>Defaults to <c>"CasCap.App"</c>.</remarks>
    [Required]
    public required string OtelServiceName { get; init; } = "CasCap.App";
}
