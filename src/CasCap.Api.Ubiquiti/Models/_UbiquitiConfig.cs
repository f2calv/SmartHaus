namespace CasCap.Models;

/// <summary>
/// Configuration options for the Ubiquiti UniFi Protect IP camera integration.
/// </summary>
public record UbiquitiConfig : IAppConfig, IAzTableStorageConfig, IMetricsConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(UbiquitiConfig)}";

    /// <summary>
    /// The base address of the UniFi Protect controller (e.g. "https://192.168.1.1").
    /// </summary>
    [Required, Url]
    public required string BaseAddress { get; init; }

    /// <summary>
    /// The username for authentication with the UniFi Protect controller.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// The password for authentication with the UniFi Protect controller.
    /// </summary>
    public string? Password { get; init; }

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
