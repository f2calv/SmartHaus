namespace CasCap.Models;

/// <summary>
/// Configuration for the signal-cli REST API integration.
/// </summary>
/// <remarks>
/// See <see href="https://bbernhard.github.io/signal-cli-rest-api/"/> for the full API specification.
/// </remarks>
public record SignalCliConfig : IAppConfig, IHealthCheckConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(SignalCliConfig)}";

    /// <summary>
    /// The transport mode used to communicate with the signal-cli REST API.
    /// Defaults to <see cref="SignalCliTransport.JsonRpc"/>.
    /// </summary>
    /// <remarks>
    /// When set to <see cref="SignalCliTransport.JsonRpc"/> or <see cref="SignalCliTransport.JsonRpcNative"/>,
    /// message reception switches from HTTP polling to a persistent WebSocket connection.
    /// <see cref="SignalCliTransport.Normal"/> and <see cref="SignalCliTransport.Native"/> use the
    /// pure REST interface implementation.
    /// Used by <see cref="CasCap.Extensions.ServiceCollectionExtensions"/>.
    /// </remarks>
    public SignalCliTransport TransportMode { get; init; } = SignalCliTransport.JsonRpc;

    /// <summary>
    /// The base address of the signal-cli REST API (e.g. "http://signalcli.monitoring.svc.cluster.local").
    /// </summary>
    [Required, Url]
    public required string BaseAddress { get; init; }

    /// <summary>
    /// The health check endpoint path. Defaults to <c>"v1/health"</c>.
    /// </summary>
    [Required]
    public required string HealthCheckUri { get; init; } = "v1/health";

    /// <summary>
    /// The Kubernetes probe type for the health check. Defaults to <see cref="KubernetesProbeTypes.Readiness"/>.
    /// </summary>
    [Required]
    public required KubernetesProbeTypes HealthCheck { get; init; } = KubernetesProbeTypes.Readiness;

    /// <summary>
    /// Per-request timeout in milliseconds for <c>POST /v2/send</c>.
    /// Defaults to 180 000 ms (3 minutes) to accommodate large attachments.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="CasCap.Services.SignalCliRestClientService"/>.
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int SendTimeoutMs { get; init; } = 180_000;

    /// <summary>
    /// The registered Signal phone number used as the sender for outgoing messages
    /// (e.g. <c>"+49151..."</c>).
    /// </summary>
    [Required, Phone]
    public required string PhoneNumber { get; init; }

    /// <summary>
    /// Optional second phone number used as the recipient for debug / diagnostic messages
    /// (e.g. <c>"+49151..."</c>).
    /// </summary>
    /// <remarks>
    /// When set, the <see cref="CasCap.Services.CommunicationsBgService"/> sends a detailed
    /// processing-stats message directly to this number after every agent run, reactivating
    /// the Signal "Note to Self" debug feed. Leave <see langword="null"/> to disable.
    /// </remarks>
    [Phone]
    public string? PhoneNumberDebug { get; init; }

    /// <summary>Maximum number of WebSocket reconnection attempts before giving up.</summary>
    /// <remarks>
    /// Defaults to <c>10</c>.
    /// Used by <see cref="CasCap.Services.SignalCliJsonRpcClientService"/>.
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int MaxReconnectAttempts { get; init; } = 10;

    /// <summary>Initial backoff delay in milliseconds for WebSocket reconnection attempts.</summary>
    /// <remarks>
    /// Defaults to <c>2000</c> ms (2 seconds). The delay doubles on each successive failure
    /// up to <see cref="MaxReconnectDelayMs"/>.
    /// Used by <see cref="CasCap.Services.SignalCliJsonRpcClientService"/>.
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int InitialReconnectDelayMs { get; init; } = 2_000;

    /// <summary>Maximum backoff delay in milliseconds for WebSocket reconnection attempts.</summary>
    /// <remarks>
    /// Defaults to <c>120000</c> ms (2 minutes).
    /// Used by <see cref="CasCap.Services.SignalCliJsonRpcClientService"/>.
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int MaxReconnectDelayMs { get; init; } = 120_000;
}
