namespace CasCap.Services;

/// <summary>Publishes a bounded camera thumbnail to the shared media stream.</summary>
/// <param name="logger">Logger.</param>
/// <param name="securityAgentConfig">Security media-cache options.</param>
/// <param name="mediaSink">Shared media stream sink.</param>
/// <param name="remoteCache">Redis cache used for bounded image bytes.</param>
public sealed class CameraThumbnailPublisher(
    ILogger<CameraThumbnailPublisher> logger,
    IOptions<SecurityAgentConfig> securityAgentConfig,
    IEventSink<MediaEvent> mediaSink,
    IRemoteCache remoteCache)
{
    /// <summary>Publishes the event thumbnail when one is available.</summary>
    /// <param name="request">Camera event and thumbnail metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task PublishThumbnail(
        CameraClipRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Thumbnail is null)
            return;

        var imageRedisKey = $"{securityAgentConfig.Value.ImageCacheKeyPrefix}:"
            + $"{request.MediaSource.ToLowerInvariant()}:{request.EventId}";
        await remoteCache.Db.StringSetAsync(
            imageRedisKey,
            request.Thumbnail,
            TimeSpan.FromMilliseconds(securityAgentConfig.Value.ImageCacheTtlMs));

        var mediaEvent = new MediaEvent
        {
            Source = request.MediaSource,
            EventType = request.EventType,
            Media = new MediaReference
            {
                MediaRedisKey = imageRedisKey,
                MimeType = request.ThumbnailMimeType ?? "image/jpeg",
            },
            MediaType = MediaType.Image,
            TimestampUtc = request.TimestampUtc,
            Metadata = request.Metadata,
        };

        logger.LogInformation(
            "{ClassName} cached {ThumbnailBytes} thumbnail bytes for {EventType}, writing to media stream",
            nameof(CameraThumbnailPublisher),
            request.Thumbnail.Length,
            request.EventType);
        await mediaSink.WriteEvent(mediaEvent, cancellationToken);
    }
}
