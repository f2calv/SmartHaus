namespace CasCap.Models;

/// <summary>Configuration for the Signalizr communications pipeline run by <see cref="CasCap.Services.CommunicationsBgService"/>.</summary>
/// <remarks>
/// These settings govern how the application interacts with its Signalizr groups. The Signal
/// account, its groups and its profile belong to the gateway.
/// </remarks>
public sealed record CommsConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(CommsConfig)}";

    /// <summary>Exact Signal group display name used for the user-facing chat: messages, reactions, typing and polls.</summary>
    /// <remarks>Preserve spaces and case.</remarks>
    [Required, MinLength(1)]
    public string GroupName { get; init; } = "My Test Group Name";

    /// <summary>
    /// Exact Signal group display name for operator diagnostics, or <see langword="null"/> to disable them.
    /// </summary>
    /// <remarks>
    /// Receives drop notices, events from <see cref="MonitorSources"/> and, when an agent responder is
    /// registered, pipeline timelines, stream-event copies, compaction notices and, when
    /// <see cref="EchoTranscriptToDebugChat"/> is enabled, voice transcripts. Its Signal group must
    /// contain only the operator, because these messages repeat other people's content. Preserve
    /// spaces and case.
    /// </remarks>
    public string? MonitorGroupName { get; init; }

    /// <summary>
    /// <see cref="CommsEvent.Source"/> values delivered to <see cref="MonitorGroupName"/> instead of
    /// <see cref="GroupName"/>.
    /// </summary>
    /// <remarks>
    /// Defaults to none. Events from these sources are operational diagnostics, so they are sent
    /// directly with their media and never become agent prompts. Ignored when
    /// <see cref="MonitorGroupName"/> is unset. Used by <see cref="CasCap.Services.MonitorSourcesGroupRouter"/>.
    /// </remarks>
    public HashSet<string> MonitorSources { get; init; } = [];

    /// <summary>Whether a chat-bound stream event becomes a turn for the registered responder.</summary>
    /// <remarks>
    /// Defaults to <see langword="true"/>, so an agent can describe or act on each event. Set it to
    /// <see langword="false"/> when stream events are notifications to deliver verbatim, such as trade
    /// confirmations; they are then formatted and sent directly, and the responder only answers inbound
    /// messages. Events routed to <see cref="MonitorGroupName"/> are always sent directly.
    /// </remarks>
    public bool StreamEventTurnsEnabled { get; init; } = true;

    /// <summary>Whether to echo a successful voice transcript to <see cref="MonitorGroupName"/>.</summary>
    /// <remarks>Defaults to <see langword="false"/>. This is communications orchestration policy, not voice processing.</remarks>
    public bool EchoTranscriptToDebugChat { get; init; }

    /// <summary>
    /// Whether an agent responder sends a separate status message (e.g. "🔀 Consulting SecurityAgent…")
    /// to the group when the agent delegates to a sub-agent.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="true"/>. When disabled, only the reaction swap (⏳ → 🔀 → ⏳ → ✅)
    /// indicates sub-agent delegation. Only read when an agent responder is registered.
    /// </remarks>
    public bool DelegationMessagesEnabled { get; init; } = true;

    /// <summary>Redis Stream key for cross-instance communication of key events.</summary>
    /// <remarks>Defaults to <c>"comms:stream:events"</c>.</remarks>
    [Required, MinLength(1)]
    public string StreamKey { get; init; } = "comms:stream:events";

    /// <summary>Redis consumer group name used to consume the communications stream.</summary>
    /// <remarks>Defaults to <c>"comms:agents"</c>.</remarks>
    [Required, MinLength(1)]
    public string ConsumerGroup { get; init; } = "comms:agents";

    /// <summary>Consumer name identifying this instance within the consumer group.</summary>
    /// <remarks>Defaults to <c>{MachineName}-{AppName}</c> for automatic per-pod uniqueness in Kubernetes.</remarks>
    [Required, MinLength(1)]
    public string ConsumerName { get; init; } = $"{Environment.MachineName}-{AppDomain.CurrentDomain.FriendlyName}";

    /// <summary>Starting ID used when creating the consumer group for the first time.</summary>
    /// <remarks>Defaults to <c>"0"</c> (read from the beginning).</remarks>
    [Required, MinLength(1)]
    public string ConsumerGroupStartId { get; init; } = "0";

    /// <summary>Redis stream read position passed to <c>XREADGROUP</c>.</summary>
    /// <remarks>Defaults to <c>"&gt;"</c> (new messages only).</remarks>
    [Required, MinLength(1)]
    public string StreamReadPosition { get; init; } = ">";

    /// <summary>Maximum entries to read from the Redis stream per <c>XREADGROUP</c> call.</summary>
    /// <remarks>Defaults to <c>10</c>.</remarks>
    [Range(1, 10_000)]
    public int StreamReadCount { get; init; } = 10;

    /// <summary>Retry interval in milliseconds for the comms stream and the Signalizr subscription.</summary>
    /// <remarks>
    /// Defaults to <c>5000</c> ms. Used with
    /// <see cref="System.Threading.Tasks.Task.Delay(System.TimeSpan, System.TimeProvider, System.Threading.CancellationToken)"/>.
    /// </remarks>
    [Range(1, 300_000)]
    public int PollingIntervalMs { get; init; } = 5_000;

    /// <summary>Delay in milliseconds between attempts to reach the Signalizr gateway at startup.</summary>
    /// <remarks>
    /// Defaults to <c>2000</c> ms. An unreachable gateway is retried rather than faulting the feature.
    /// Used with <see cref="SignalizrClientExtensions.WaitForGroupsAsync"/>.
    /// </remarks>
    [Range(1, 300_000)]
    public int HealthCheckProbeDelayMs { get; init; } = 2_000;

    /// <summary>Optional set of <see cref="CommsEvent.Source"/> values this instance will process.</summary>
    /// <remarks>
    /// When non-empty, stream entries whose <c>Source</c> is not in this set are acknowledged
    /// but silently skipped. This provides defense-in-depth isolation when multiple services
    /// share the same Redis database. When empty (default), all sources are processed.
    /// </remarks>
    public HashSet<string> AllowedSources { get; init; } = [];

    /// <summary>Whether producer-driven stream events are rate-limited before being forwarded to the group.</summary>
    /// <remarks>
    /// Defaults to <see langword="true"/>. When a burst of <see cref="CommsEvent"/> entries arrives faster than
    /// <see cref="StreamSendRatePerMinute"/> (with an initial allowance of <see cref="StreamSendBurst"/>),
    /// excess events are acknowledged but dropped rather than queued for slow drip-feed delivery by
    /// the gateway. Interactive replies to user messages are never throttled by this setting.
    /// </remarks>
    public bool StreamSendThrottlingEnabled { get; init; } = true;

    /// <summary>Sustained number of stream-originated messages that may be forwarded to the group per minute.</summary>
    /// <remarks>Defaults to <c>20</c>. Token-bucket replenishment rate.</remarks>
    [Range(1, 10_000)]
    public int StreamSendRatePerMinute { get; init; } = 20;

    /// <summary>Maximum initial burst of stream-originated messages allowed before throttling engages.</summary>
    /// <remarks>Defaults to <c>10</c>. Token-bucket capacity.</remarks>
    [Range(1, 10_000)]
    public int StreamSendBurst { get; init; } = 10;

    /// <summary>Whether stream events older than <see cref="MaxEventAgeMs"/> are dropped instead of delivered late.</summary>
    /// <remarks>
    /// Defaults to <see langword="true"/>. A producer flood or consumer backlog can leave events queued far longer
    /// than they are useful; stale events are acknowledged but dropped rather than drip-fed to the group.
    /// </remarks>
    public bool StaleEventDroppingEnabled { get; init; } = true;

    /// <summary>Maximum age in milliseconds of a stream event before it is considered stale and dropped.</summary>
    /// <remarks>
    /// Defaults to <c>60000</c> ms (60 seconds). Compared against <see cref="CommsEvent.TimestampUtc"/> at the
    /// moment of processing, with <see cref="System.TimeSpan.FromMilliseconds(double)"/>.
    /// </remarks>
    [Range(1_000, 86_400_000)]
    public int MaxEventAgeMs { get; init; } = 60_000;

    /// <summary>Minimum interval in milliseconds between drop-notice messages while messages are being dropped.</summary>
    /// <remarks>
    /// Defaults to <c>60000</c> ms (1 minute). Covers both rate-limited and stale drops. The first drop
    /// always emits a notice immediately. Used with <see cref="System.TimeSpan.FromMilliseconds(double)"/>.
    /// </remarks>
    [Range(1, 3_600_000)]
    public int DropNoticeIntervalMs { get; init; } = 60_000;

    /// <summary>Maximum number of turns buffered in the reply queue before producers wait for space.</summary>
    /// <remarks>
    /// Defaults to <c>100</c>. Bounds the queue so a flood cannot grow an unbounded backlog that the
    /// gateway drip-feeds for hours; when full, a producer waits rather than evicting an accepted turn.
    /// </remarks>
    [Range(1, 100_000)]
    public int ReplyQueueCapacity { get; init; } = 100;

    /// <summary>Redis key prefix under which producers cache comms attachment bytes.</summary>
    /// <remarks>
    /// Defaults to <c>"comms:cache:media"</c>. Each attachment is stored at <c>{prefix}:{id}</c> and
    /// referenced from <see cref="CommsEvent.JsonPayload"/> through a <see cref="MediaReference"/>.
    /// Used by <see cref="CasCap.Services.CommsMediaStore"/>.
    /// </remarks>
    [Required, MinLength(1)]
    public string MediaCacheKeyPrefix { get; init; } = "comms:cache:media";

    /// <summary>Time in milliseconds a cached attachment remains in Redis before it expires unsent.</summary>
    /// <remarks>
    /// Defaults to <c>300000</c> ms (5 minutes), well beyond <see cref="MaxEventAgeMs"/>, so an event
    /// that is still fresh enough to deliver always finds its attachment. The consumer deletes the key
    /// once sent. Used by <see cref="CasCap.Services.CommsMediaStore"/> with
    /// <see cref="System.TimeSpan.FromMilliseconds(double)"/>.
    /// </remarks>
    [Range(1_000, 86_400_000)]
    public int MediaCacheTtlMs { get; init; } = 300_000;

    /// <summary>How long, in hours, an inbound message identity is reserved for duplicate suppression.</summary>
    /// <remarks>
    /// Defaults to <c>168</c> (7 days). A redelivery of the same message within this window is not
    /// processed again. Used by <see cref="CasCap.Services.RedisSignalMessageDeduplicator"/>.
    /// </remarks>
    [Range(1, 720)]
    public int MessageDeduplicationTtlHours { get; init; } = 168;
}
