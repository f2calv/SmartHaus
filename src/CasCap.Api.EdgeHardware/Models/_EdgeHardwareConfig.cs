namespace CasCap.Models;

/// <summary>
/// Unified configuration for edge hardware monitoring: GPU metrics, CPU temperature,
/// Raspberry Pi camera/sensors, energy estimation, and event sinks.
/// </summary>
/// <remarks>
/// Replaces the former <c>GpuMonitorConfig</c> and <c>RaspberryPiConfig</c>.
/// Used by <see cref="CasCap.Services.EdgeHardwareMonitorBgService"/> for GPU/CPU polling,
/// <see cref="CasCap.Services.PiCameraDeviceService"/> for camera captures,
/// <see cref="CasCap.Services.GpioHcsr501SensorService"/> for motion detection,
/// and by the stats footer for energy comparison formatting.
/// </remarks>
public record EdgeHardwareConfig : IAppConfig, IAzTableStorageConfig, IMetricsConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(EdgeHardwareConfig)}";

    // ── GPU polling ──────────────────────────────────────────────────

    /// <summary>
    /// Polling interval in milliseconds for nvidia-smi GPU metric reads.
    /// </summary>
    /// <remarks>Defaults to 2000 ms.</remarks>
    [Range(1, int.MaxValue)]
    public int PollIntervalMs { get; init; } = 2_000;

    // ── Raspberry Pi ─────────────────────────────────────────────────

    /// <summary>
    /// Local filesystem path for camera captures and other file output.
    /// Used by <see cref="CasCap.Services.PiCameraDeviceService"/>.
    /// </summary>
    public string? LocalPath { get; init; }

    /// <summary>
    /// Sensor hardware configuration.
    /// Used by <see cref="CasCap.Services.GpioHcsr501SensorService"/>.
    /// </summary>
    /// <remarks>Defaults to a new <see cref="Sensors"/> instance.</remarks>
    [ValidateObjectMembers]
    public Sensors Sensors { get; init; } = new();

    // ── Energy estimation ────────────────────────────────────────────

    /// <summary>
    /// Fallback energy estimate in watt-hours per 1 000 tokens when live GPU
    /// power monitoring is unavailable. Set to zero to disable token-based estimation.
    /// </summary>
    /// <remarks>Defaults to 0.003 Wh/kTok (typical for small models on edge GPUs).</remarks>
    [Range(0.0, double.MaxValue)]
    public double EnergyPerKiloTokenWh { get; init; } = 0.003;

    /// <summary>
    /// Reference energy in watt-hours for boiling a full kettle (~2 kW × 3 min).
    /// </summary>
    /// <remarks>Defaults to 100 Wh.</remarks>
    [Range(0.001, double.MaxValue)]
    public double KettleBoilWh { get; init; } = 100;

    /// <summary>
    /// Reference energy in watt-hours for a full phone charge.
    /// </summary>
    /// <remarks>Defaults to 15 Wh.</remarks>
    [Range(0.001, double.MaxValue)]
    public double PhoneChargeWh { get; init; } = 15;

    /// <summary>
    /// Reference energy in watt-hours for one hour of a typical LED bulb.
    /// </summary>
    /// <remarks>Defaults to 10 Wh.</remarks>
    [Range(0.001, double.MaxValue)]
    public double LedBulbHourWh { get; init; } = 10;

    // ── Metrics ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>Defaults to <c>"haus"</c>.</remarks>
    [Required]
    public required string MetricNamePrefix { get; init; } = "haus";

    /// <inheritdoc/>
    /// <remarks>Defaults to <c>"CasCap.App"</c>.</remarks>
    [Required]
    public required string OtelServiceName { get; init; } = "CasCap.App";

    // ── Sinks

    /// <inheritdoc/>
    [Required]
    public required string AzureTableStorageConnectionString { get; init; }

    /// <inheritdoc/>
    /// <remarks>Defaults to <see cref="KubernetesProbeTypes.Startup"/>.</remarks>
    [Required]
    public required KubernetesProbeTypes HealthCheckAzureTableStorage { get; init; } = KubernetesProbeTypes.None;

    /// <summary>Expiry in days applied to Redis series sorted set keys.</summary>
    /// <remarks>Defaults to <c>7</c>.</remarks>
    [Range(1, int.MaxValue)]
    public int RedisSeriesExpiryDays { get; init; } = 7;

    /// <summary>
    /// Configuration for event data sinks.
    /// </summary>
    [Required, ValidateObjectMembers]
    public required SinkConfig Sinks { get; init; }

    // ── GPU alert ────────────────────────────────────────────────────

    /// <summary>
    /// GPU temperature threshold in degrees Celsius above which the comms stream sink
    /// writes an overtemperature alert event.
    /// Used by <see cref="CasCap.Services.EdgeHardwareSinkCommsStreamService"/>.
    /// </summary>
    /// <remarks>Defaults to <c>70.0</c>°C.</remarks>
    [Range(0.0, 150.0)]
    public double GpuAlertThresholdC { get; init; } = 70.0;

    /// <summary>
    /// Hysteresis band in degrees Celsius applied below <see cref="GpuAlertThresholdC"/> before the
    /// alert can re-arm. Once the alert fires, GPU temperature must drop below
    /// <c>GpuAlertThresholdC − GpuAlertHysteresis</c> before a new alert is possible.
    /// Used by <see cref="CasCap.Services.EdgeHardwareSinkCommsStreamService"/>.
    /// </summary>
    /// <remarks>Defaults to <c>5.0</c>°C.</remarks>
    [Range(0.0, 50.0)]
    public double GpuAlertHysteresis { get; init; } = 5.0;

    /// <summary>
    /// Minimum cooldown in milliseconds between successive GPU overtemperature alert events.
    /// Even if the hysteresis condition is met, no new alert fires until this
    /// interval has elapsed since the last alert.
    /// Used by <see cref="CasCap.Services.EdgeHardwareSinkCommsStreamService"/>.
    /// </summary>
    /// <remarks>Defaults to <c>300000</c> (5 minutes).</remarks>
    [Range(1, int.MaxValue)]
    public int GpuAlertCooldownMs { get; init; } = 300_000;
}
