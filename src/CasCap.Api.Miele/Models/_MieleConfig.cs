namespace CasCap.Models;

/// <summary>
/// Configuration options for the Miele cloud API integration.
/// </summary>
public record MieleConfig : IAppConfig, IHealthCheckConfig
{
    /// <summary>Initializes a new instance of the <see cref="MieleConfig"/> record.</summary>
    [SetsRequiredMembers]
    public MieleConfig() { }

    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(MieleConfig)}";

    /// <summary>OAuth bearer token for Miele API authentication.</summary>
    [Required, MinLength(1)]
    public required string OAuthToken { get; init; } = default!;

    /// <summary>
    /// The health check endpoint URI for the Miele API. Defaults to <c>"https://api.mcs3.miele.com/v1/"</c>.
    /// </summary>
    [Required, Url]
    public required string HealthCheckUri { get; init; } = "https://api.mcs3.miele.com/v1/";

    /// <summary>
    /// The HTTP status codes considered healthy for the Miele API endpoint. Defaults to <c>[404]</c>.
    /// </summary>
    [Required]
    public required IReadOnlyList<int> HealthCheckExpectedHttpStatusCodes { get; init; } = [404];

    /// <inheritdoc cref="KubernetesProbeTypes"/>
    [Required]
    public required KubernetesProbeTypes HealthCheck { get; init; } = KubernetesProbeTypes.Readiness;

    /// <summary>
    /// Delay in milliseconds between each poll when waiting for the Miele connection to
    /// become active at startup. Used by <see cref="CasCap.Services.MieleEventStreamBgService"/>.
    /// Defaults to <c>1000</c> ms.
    /// </summary>
    [Required, Range(1, int.MaxValue)]
    public required int ConnectionPollingDelayMs { get; init; } = 1_000;

    /// <summary>
    /// Number of connection polling attempts between each escalation from
    /// <see cref="Microsoft.Extensions.Logging.LogLevel.Trace"/> to
    /// <see cref="Microsoft.Extensions.Logging.LogLevel.Warning"/>. Used by
    /// <see cref="CasCap.Services.MieleEventStreamBgService"/>. Defaults to <c>10</c>.
    /// </summary>
    [Required, Range(1, int.MaxValue)]
    public required int ConnectionLogEscalationInterval { get; init; } = 10;

    /// <summary>Azure Table Storage connection string for Miele sinks.</summary>
    public string? AzureTableStorageConnectionString { get; init; }

    /// <summary>
    /// The full URL of the Miele Server-Sent Events stream endpoint.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>"https://api.mcs3.miele.com/v1/devices/all/events"</c>.
    /// Used by <see cref="CasCap.Services.MieleEventStreamBgService"/>.
    /// </remarks>
    [Required, Url]
    public required string EventStreamUrl { get; init; } = "https://api.mcs3.miele.com/v1/devices/all/events";

    /// <summary>
    /// Delay in milliseconds before reconnecting to the SSE stream after it completes or disconnects.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>60000</c> ms (60 seconds).
    /// Used by <see cref="CasCap.Services.MieleEventStreamBgService"/>.
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int EventStreamReconnectDelayMs { get; init; } = 60_000;

    /// <summary>Number of days to retain time-series data in Redis sorted sets.</summary>
    /// <remarks>Defaults to 7.</remarks>
    [Required, Range(1, int.MaxValue)]
    public required int RedisSeriesExpiryDays { get; init; } = 7;

    /// <summary>Sink configuration for event persistence.</summary>
    [Required, ValidateObjectMembers]
    public required SinkConfig Sinks { get; init; } = default!;
}
