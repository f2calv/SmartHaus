namespace CasCap.Models;

/// <summary>
/// Configuration for the security agent that performs DoorBird vision analysis
/// and posts findings to the comms stream.
/// </summary>
/// <remarks>
/// These settings are consumed by <see cref="CasCap.Services.MediaBgService"/>
/// and <see cref="CasCap.Services.DoorBirdSinkMediaStreamService"/> — they are
/// application-level orchestration concerns rather than DoorBird device settings.
/// Bound from the <c>Settings</c> sub-section of <see cref="AgentKeys.SecurityAgent"/>
/// in <c>AIConfig.Agents</c>.
/// </remarks>
public record SecurityAgentConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName =>
        $"{nameof(CasCap)}:{nameof(AgentKeys.AIConfig)}:{nameof(AgentKeys.Agents)}:{nameof(AgentKeys.SecurityAgent)}:{nameof(AgentKeys.Settings)}";

    /// <summary>
    /// Redis key prefix used when caching captured image bytes for downstream vision analysis.
    /// The full key is <c>{prefix}:{eventId}</c>.
    /// Used by <see cref="CasCap.Services.DoorBirdSinkMediaStreamService"/>.
    /// </summary>
    [Required, MinLength(1)]
    public required string ImageCacheKeyPrefix { get; init; }

    /// <summary>
    /// Time-to-live in milliseconds for cached image bytes in Redis.
    /// Used by <see cref="CasCap.Services.DoorBirdSinkMediaStreamService"/>.
    /// </summary>
    /// <remarks>Defaults to <c>300000</c> (5 minutes).</remarks>
    [Range(1, int.MaxValue)]
    public int ImageCacheTtlMs { get; init; } = 300_000;
}
