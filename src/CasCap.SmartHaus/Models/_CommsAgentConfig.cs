namespace CasCap.Models;

/// <summary>
/// Configuration for the communications agent orchestration layer.
/// </summary>
/// <remarks>
/// These settings govern how <see cref="CasCap.Services.CommunicationsBgService"/> interacts
/// with the notification group — they are application-level concerns rather than signal-cli
/// client settings.
/// Bound from the <c>Settings</c> sub-section of <see cref="AgentKeys.CommsAgent"/>
/// in <c>AIConfig.Agents</c>.
/// </remarks>
public record CommsAgentConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName =>
        $"{nameof(CasCap)}:{nameof(AgentKeys.AIConfig)}:{nameof(AgentKeys.Agents)}:{nameof(AgentKeys.CommsAgent)}:{nameof(AgentKeys.Settings)}";

    /// <summary>
    /// The name of the Signal group used for notifications.
    /// </summary>
    [Required, MinLength(1)]
    public required string GroupName { get; init; }

    /// <summary>Pre-configured Signal group ID used as a fallback when <c>ListGroups</c> fails.</summary>
    /// <remarks>
    /// When set, <see cref="CasCap.Services.CommunicationsBgService"/> will use this value
    /// instead of resolving the group ID via the signal-cli <c>listGroups</c> API. This
    /// provides resilience against signal-cli recipient store corruption that can cause
    /// <c>listGroups</c> to fail with <c>"Failed read recipient store"</c>.
    /// </remarks>
    public string? GroupId { get; init; }

    /// <summary>Signal profile display name set on each application start.</summary>
    /// <remarks>
    /// The active model name is appended automatically at startup, e.g. "Smart Haus (qwen3.5:9b)".
    /// Used by <see cref="CasCap.Services.CommunicationsBgService"/>.
    /// </remarks>
    public string ProfileName { get; init; } = "Smart Haus";

    /// <summary>
    /// Whether to send a separate status message (e.g. "🔀 Consulting SecurityAgent…") to the
    /// Signal group when the agent delegates to a sub-agent.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>true</c>. When disabled, only the reaction swap (⏳ → 🔀 → ⏳ → ✅)
    /// indicates sub-agent delegation.
    /// Used by <see cref="CasCap.Services.CommunicationsBgService"/>.
    /// </remarks>
    public bool DelegationMessagesEnabled { get; init; } = true;

    /// <summary>Redis Stream key for cross-instance communication of key events.</summary>
    /// <remarks>Defaults to <c>"comms:stream:events"</c>. Used by <see cref="CasCap.Services.CommunicationsBgService"/>.</remarks>
    [Required, MinLength(1)]
    public string StreamKey { get; init; } = "comms:stream:events";

    /// <summary>Redis consumer group name used to consume the communications stream.</summary>
    /// <remarks>Defaults to <c>"comms:agents"</c>. Used by <see cref="CasCap.Services.CommunicationsBgService"/>.</remarks>
    [Required, MinLength(1)]
    public string ConsumerGroup { get; init; } = "comms:agents";

    /// <summary>Consumer name identifying this instance within the consumer group.</summary>
    /// <remarks>Defaults to <c>"comms-0"</c>. Used by <see cref="CasCap.Services.CommunicationsBgService"/>.</remarks>
    [Required, MinLength(1)]
    public string ConsumerName { get; init; } = "comms-0";

    /// <summary>Starting ID used when creating the consumer group for the first time.</summary>
    /// <remarks>Defaults to <c>"0"</c> (read from the beginning). Used by <see cref="CasCap.Services.CommunicationsBgService"/>.</remarks>
    [Required, MinLength(1)]
    public string ConsumerGroupStartId { get; init; } = "0";

    /// <summary>Redis stream read position passed to <c>XREADGROUP</c>.</summary>
    /// <remarks>Defaults to <c>"&gt;"</c> (new messages only). Used by <see cref="CasCap.Services.CommunicationsBgService"/>.</remarks>
    [Required, MinLength(1)]
    public string StreamReadPosition { get; init; } = ">";

    /// <summary>Maximum entries to read from the Redis stream per <c>XREADGROUP</c> call.</summary>
    /// <remarks>Defaults to <c>10</c>. Used by <see cref="CasCap.Services.CommunicationsBgService"/>.</remarks>
    [Range(1, int.MaxValue)]
    public int StreamReadCount { get; init; } = 10;

    /// <summary>
    /// Polling interval in milliseconds for the comms stream and REST message retrieval.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>5000</c> (5 seconds).
    /// Used by <see cref="CasCap.Services.CommunicationsBgService"/>.
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int PollingIntervalMs { get; init; } = 5_000;

    /// <summary>
    /// Delay in milliseconds between each probe when waiting for the signal-cli
    /// readiness health check to pass at startup.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>2000</c> ms (2 seconds).
    /// Used by <see cref="CasCap.Services.CommunicationsBgService"/>.
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int HealthCheckProbeDelayMs { get; init; } = 2_000;

    /// <summary>
    /// Timeout in milliseconds for flushing pending envelopes from signal-cli at startup.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>5000</c> ms (5 seconds).
    /// Used by <see cref="CasCap.Services.CommunicationsBgService"/>.
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int FlushTimeoutMs { get; init; } = 5_000;

    /// <summary>
    /// Delay in milliseconds between polls when waiting for the Signal group to be resolved
    /// before delivering a stream event.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>1000</c> ms (1 second).
    /// Used by <see cref="CasCap.Services.CommunicationsBgService"/>.
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int GroupResolutionPollingDelayMs { get; init; } = 1_000;
}
