namespace CasCap.Models;

/// <summary>
/// Configuration options for the DoorBird door entry system integration.
/// </summary>
public sealed record DoorBirdConfig : IAppConfig, IHealthCheckConfig, IAzBlobStorageConfig, IAzTableStorageConfig, IMetricsConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(DoorBirdConfig)}";

    /// <summary>
    /// The health check endpoint path on the DoorBird device. Defaults to <c>"bha-api/info.cgi"</c>.
    /// </summary>
    [Required]
    public required string HealthCheckUri { get; init; } = "bha-api/info.cgi";

    /// <inheritdoc cref="KubernetesProbeTypes"/>
    [Required]
    public required KubernetesProbeTypes HealthCheck { get; init; } = KubernetesProbeTypes.Readiness;

    /// <summary>
    /// The base address of the DoorBird device (e.g. "http://192.168.1.248").
    /// </summary>
    [Required, Url]
    public required string BaseAddress { get; init; }

    /// <summary>
    /// The username for authentication with the DoorBird device.
    /// </summary>
    [Required, MinLength(1)]
    public required string Username { get; init; }

    /// <summary>
    /// The password for authentication with the DoorBird device.
    /// </summary>
    [Required, MinLength(1)]
    public required string Password { get; init; }

    /// <summary>Relative DoorBird LAN API path for the receive-only microphone stream.</summary>
    /// <remarks>Defaults to <c>bha-api/audio-receive.cgi</c>.</remarks>
    [Required, MinLength(1)]
    public string AudioReceiveUri { get; init; } = "bha-api/audio-receive.cgi";

    /// <summary>Maximum duration in seconds accepted by a microphone capture request.</summary>
    /// <remarks>Defaults to 30 seconds.</remarks>
    [Range(1, 300)]
    public int AudioCaptureMaxDurationSeconds { get; init; } = 30;

    /// <summary>Maximum WAV byte count returned by one microphone capture.</summary>
    /// <remarks>Defaults to 512 KiB, including the WAV header.</remarks>
    [Range(59, 10 * 1024 * 1024)]
    public int AudioCaptureMaxBytes { get; init; } = 512 * 1024;

    /// <summary>Total time budget in milliseconds for one microphone capture.</summary>
    /// <remarks>Defaults to 45 seconds.</remarks>
    [Range(1_000, 300_000)]
    public int AudioCaptureTimeoutMs { get; init; } = 45_000;

    /// <summary>
    /// The DoorBird door controller identifier.
    /// </summary>
    [Required, MinLength(1)]
    public required string DoorControllerID { get; init; }

    /// <summary>
    /// The DoorBird door controller relay identifier.
    /// </summary>
    [Required, MinLength(1)]
    public required string DoorControllerRelayID { get; init; }

    /// <inheritdoc/>
    [Required]
    public required string AzureBlobStorageConnectionString { get; init; }

    /// <summary>Azure Blob Storage container name for DoorBird images.</summary>
    [Required]
    public required string AzureBlobStorageContainerName { get; init; }

    /// <inheritdoc/>
    [Required]
    public required KubernetesProbeTypes HealthCheckAzureBlobStorage { get; init; } = KubernetesProbeTypes.None;

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

    /// <inheritdoc/>
    /// <remarks>Defaults to <c>"haus"</c>.</remarks>
    [Required]
    public required string MetricNamePrefix { get; init; } = "haus";

    /// <inheritdoc/>
    /// <remarks>Defaults to <c>"CasCap.App"</c>.</remarks>
    [Required]
    public required string OtelServiceName { get; init; } = "CasCap.App";
}
