namespace CasCap.Services;

/// <summary>
/// Writes <see cref="DoorBirdEvent"/> metadata to the media Redis Stream via
/// <see cref="IEventSink{T}"/> so that <see cref="MediaBgService"/> can
/// route the image to the appropriate domain agent for analysis.
/// </summary>
[SinkType("MediaStream")]
public class DoorBirdSinkMediaStreamService(ILogger<DoorBirdSinkMediaStreamService> logger,
    IOptions<SecurityAgentConfig> securityAgentConfig,
    IEventSink<MediaEvent> mediaSink,
    IRemoteCache remoteCache) : IEventSink<DoorBirdEvent>
{
    /// <inheritdoc/>
    public IAsyncEnumerable<DoorBirdEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public async Task WriteEvent(DoorBirdEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} {@Dbe}", nameof(DoorBirdSinkMediaStreamService), @event);
        if (@event.bytes is null) return;

        // Cache image bytes in Redis for downstream media analysis.
        var imageRedisKey = $"{securityAgentConfig.Value.ImageCacheKeyPrefix}:{@event.EventId}";
        await remoteCache.Db.StringSetAsync(imageRedisKey, @event.bytes,
            TimeSpan.FromMilliseconds(securityAgentConfig.Value.ImageCacheTtlMs));

        var mediaEvent = new MediaEvent
        {
            Source = "DoorBird",
            EventType = @event.DoorBirdEventType.ToString(),
            Media = new MediaReference
            {
                MediaRedisKey = imageRedisKey,
                MimeType = "image/jpeg",
            },
            MediaType = MediaType.Image,
            TimestampUtc = @event.DateCreatedUtc,
            Metadata = (@event with { bytes = null }).ToJson(),
        };

        logger.LogInformation("{ClassName} event detected {DoorBirdEvent}, writing to media stream",
            nameof(DoorBirdSinkMediaStreamService), @event);
        await mediaSink.WriteEvent(mediaEvent, cancellationToken);
    }
}
