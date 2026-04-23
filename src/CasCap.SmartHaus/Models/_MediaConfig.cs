namespace CasCap.Models;

/// <summary>
/// Configuration for <see cref="CasCap.Services.MediaBgService"/>.
/// </summary>
/// <remarks>
/// Bound from the <c>CasCap:MediaConfig</c> section under the application configuration root.
/// </remarks>
public record MediaConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(MediaConfig)}";

    /// <summary>
    /// Maps a media source identifier (e.g. <c>"DoorBird"</c>) to the agent key
    /// that should analyse events from that source (e.g. <c>"SecurityAgent"</c>).
    /// </summary>
    /// <remarks>Defaults to an empty dictionary; sources not mapped are logged and skipped.</remarks>
    public Dictionary<string, string> SourceAgentMap { get; init; } = [];

    /// <summary>Redis Stream key for source-agnostic media events (images, audio, documents).</summary>
    /// <remarks>Defaults to <c>"media:stream:events"</c>. Used by <see cref="CasCap.Services.MediaBgService"/>.</remarks>
    [Required, MinLength(1)]
    public string StreamKey { get; init; } = "media:stream:events";

    /// <summary>Redis consumer group name used to consume the media analysis stream.</summary>
    /// <remarks>Defaults to <c>"media:processors"</c>. Used by <see cref="CasCap.Services.MediaBgService"/>.</remarks>
    [Required, MinLength(1)]
    public string ConsumerGroup { get; init; } = "media:processors";

    /// <summary>Consumer name identifying this instance within the consumer group.</summary>
    /// <remarks>Defaults to <c>"media-0"</c>. Used by <see cref="CasCap.Services.MediaBgService"/>.</remarks>
    [Required, MinLength(1)]
    public string ConsumerName { get; init; } = "media-0";

    /// <summary>Starting ID used when creating the consumer group for the first time.</summary>
    /// <remarks>Defaults to <c>"0"</c> (read from the beginning). Used by <see cref="CasCap.Services.MediaBgService"/>.</remarks>
    [Required, MinLength(1)]
    public string ConsumerGroupStartId { get; init; } = "0";

    /// <summary>Redis stream read position passed to <c>XREADGROUP</c>.</summary>
    /// <remarks>Defaults to <c>"&gt;"</c> (new messages only). Used by <see cref="CasCap.Services.MediaBgService"/>.</remarks>
    [Required, MinLength(1)]
    public string StreamReadPosition { get; init; } = ">";

    /// <summary>Maximum entries to read from the Redis stream per <c>XREADGROUP</c> call.</summary>
    /// <remarks>Defaults to <c>10</c>. Used by <see cref="CasCap.Services.MediaBgService"/>.</remarks>
    [Range(1, int.MaxValue)]
    public int StreamReadCount { get; init; } = 10;

    /// <summary>
    /// Polling interval in milliseconds when no new stream entries are available.
    /// </summary>
    /// <remarks>Defaults to <c>1000</c> (1 second).</remarks>
    [Range(100, int.MaxValue)]
    public int PollingIntervalMs { get; init; } = 1_000;
}
