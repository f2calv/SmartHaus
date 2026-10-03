namespace CasCap.Services;

/// <summary>Routes UniFi Protect thumbnail events through the security media-analysis pipeline.</summary>
[SinkType("MediaStream")]
public sealed class UbiquitiSinkMediaStreamService(
    ILogger<UbiquitiSinkMediaStreamService> logger,
    IOptions<SecurityAgentConfig> securityAgentConfig,
    IEventSink<MediaEvent> mediaSink,
    IRemoteCache remoteCache) : IEventSink<UbiquitiEvent>
{
    /// <inheritdoc/>
    public string SinkType => "MediaStream";

    /// <inheritdoc/>
    public async Task WriteEvent(UbiquitiEvent @event, CancellationToken cancellationToken = default)
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
            Metadata = (@event with { Thumbnail = null }).ToJson(),
        };

        logger.LogInformation(
            "{ClassName} cached {ThumbnailBytes} thumbnail bytes for {EventType}, writing to media stream",
            nameof(UbiquitiSinkMediaStreamService),
            @event.Thumbnail.Length,
            @event.UbiquitiEventType);
        await mediaSink.WriteEvent(mediaEvent, cancellationToken);
    }
}
