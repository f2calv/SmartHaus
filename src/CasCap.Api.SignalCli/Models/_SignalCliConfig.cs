namespace CasCap.Models;

/// <summary>
/// Configuration for the signal-cli REST API integration.
/// </summary>
/// <remarks>
/// See <see href="https://bbernhard.github.io/signal-cli-rest-api/"/> for the full API specification.
/// </remarks>
public sealed record SignalCliConfig : IAppConfig, IHealthCheckConfig
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
    public string HealthCheckUri { get; init; } = "v1/health";

    /// <summary>
    /// The Kubernetes probe type for the health check. Defaults to <see cref="KubernetesProbeTypes.Readiness"/>.
    /// </summary>
    [Required]
    public KubernetesProbeTypes HealthCheck { get; init; } = KubernetesProbeTypes.Readiness;

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

    /// <summary>Bounded capacity of the internal message channel.</summary>
    /// <remarks>
    /// Defaults to <c>256</c>. When the channel is full the WebSocket receive loop applies
    /// back-pressure (waits) until a consumer drains messages via <c>ReceiveAsync</c>.
    /// Used by <see cref="CasCap.Services.SignalCliJsonRpcClientService"/>.
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int ChannelCapacity { get; init; } = 256;

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

    /// <summary>
    /// Maximum time in milliseconds the WebSocket receive stream may remain silent (no inbound
    /// frames) before the staleness watchdog logs an error and forces a reconnect. Set to <c>0</c>
    /// to disable the watchdog (the default).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Detects the failure mode where the signal-cli daemon's receive thread has died (e.g. a
    /// poisoned <c>msg-cache</c> envelope) yet the WebSocket and the daemon process remain healthy:
    /// outbound sending keeps working while inbound delivery silently stops indefinitely. Neither
    /// the WebSocket reconnect loop nor a process restart detect this, so it would otherwise go
    /// unnoticed.
    /// </para>
    /// <para>
    /// <b>Heuristic — tune to your traffic.</b> The watchdog only observes inbound frames, so a
    /// genuinely quiet account that legitimately receives nothing for a long period would trip a
    /// short timeout. Set this comfortably above the longest expected gap between inbound messages
    /// (e.g. several hours), or leave at <c>0</c> when no reliable inbound cadence exists. Pairs
    /// with the server-side <c>msg-cache</c> quarantine mitigation, since a reconnect alone does
    /// not clear a poisoned cache.
    /// </para>
    /// Used by <see cref="CasCap.Services.SignalCliJsonRpcClientService"/>.
    /// </remarks>
    [Range(0, int.MaxValue)]
    public int ReceiveStalenessTimeoutMs { get; init; }

    /// <summary>Whether to attach HTTP Basic credentials from <see cref="ApiAuthConfig"/> to outgoing requests.</summary>
    /// <remarks>
    /// Defaults to <see langword="false"/>. Set to <see langword="true"/> when the signal-cli REST API is
    /// exposed behind a reverse proxy with Basic authentication (e.g. cross-cluster access via an ingress).
    /// Used by <see cref="CasCap.Extensions.ServiceCollectionExtensions"/>.
    /// </remarks>
    public bool BasicAuthEnabled { get; init; }
}
