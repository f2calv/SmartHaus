namespace CasCap.Services;

/// <summary>Publishes a bounded Ubiquiti webhook thumbnail to the shared media stream.</summary>
/// <param name="logger">Logger.</param>
/// <param name="securityAgentConfig">Security media-cache options.</param>
/// <param name="mediaSink">Shared media stream sink.</param>
/// <param name="remoteCache">Redis cache used for bounded image bytes.</param>
public sealed class UbiquitiMediaPublisher(
    ILogger<UbiquitiMediaPublisher> logger,
    IOptions<SecurityAgentConfig> securityAgentConfig,
    IEventSink<MediaEvent> mediaSink,
    IRemoteCache remoteCache)
{
    /// <summary>Publishes the event thumbnail when one is available.</summary>
    /// <param name="event">Ubiquiti event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PublishThumbnail(
        UbiquitiEvent @event,
        CancellationToken cancellationToken = default)
    {
        if (@event.Thumbnail is null)
            return;

        var imageRedisKey = $"{securityAgentConfig.Value.ImageCacheKeyPrefix}:ubiquiti:{@event.EventId}";
        await remoteCache.Db.StringSetAsync(
            imageRedisKey,
            @event.Thumbnail,
            TimeSpan.FromMilliseconds(securityAgentConfig.Value.ImageCacheTtlMs));

        var mediaEvent = new MediaEvent
        {
            Source = "Ubiquiti",
            EventType = @event.UbiquitiEventType.ToString(),
            Media = new MediaReference
            {
                MediaRedisKey = imageRedisKey,
                MimeType = @event.ThumbnailMimeType ?? "image/jpeg",
            },
            MediaType = MediaType.Image,
            TimestampUtc = @event.DateCreatedUtc,
            Metadata = (@event with { Thumbnail = null, SourceCameraId = null }).ToJson(),
        };

        logger.LogInformation(
            "{ClassName} cached {ThumbnailBytes} thumbnail bytes for {EventType}, writing to media stream",
            nameof(UbiquitiMediaPublisher),
            @event.Thumbnail.Length,
            @event.UbiquitiEventType);
        await mediaSink.WriteEvent(mediaEvent, cancellationToken);
    }
}
