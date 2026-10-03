namespace CasCap.Services;

/// <summary>
/// Writes <see cref="DoorBirdEvent"/> metadata to the media Redis Stream via
/// <see cref="IEventSink{T}"/> so that <see cref="MediaBgService"/> can
/// route the image to the appropriate domain agent for analysis.
/// </summary>
[SinkType("MediaStream")]
public sealed class DoorBirdSinkMediaStreamService(ILogger<DoorBirdSinkMediaStreamService> logger,
    CameraClipQueue clipQueue,
    CameraThumbnailPublisher thumbnailPublisher) : IEventSink<DoorBirdEvent>
{
    /// <inheritdoc/>
    public string SinkType => "MediaStream";


    /// <inheritdoc/>
    public async Task WriteEvent(DoorBirdEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "{ClassName} processing EventType={EventType}, SnapshotBytes={SnapshotBytes}",
            nameof(DoorBirdSinkMediaStreamService),
            @event.DoorBirdEventType,
            @event.bytes?.Length ?? 0);
        if (@event.bytes is null)
            return;

        var admission = clipQueue.TryEnqueue(@event);
        if (admission is CameraClipAdmission.Enqueued)
        {
            logger.LogInformation("{ClassName} queued {EventType} clip capture",
                nameof(DoorBirdSinkMediaStreamService), @event.DoorBirdEventType);
            return;
        }

        if (admission is CameraClipAdmission.Suppressed)
        {
            logger.LogDebug("{ClassName} suppressed a repeated {EventType} clip event",
                nameof(DoorBirdSinkMediaStreamService), @event.DoorBirdEventType);
            return;
        }

        if (admission is CameraClipAdmission.QueueFull)
        {
            logger.LogWarning("{ClassName} clip queue is full; publishing thumbnail fallback",
                nameof(DoorBirdSinkMediaStreamService));
        }

        var source = new CameraClipSourceConfig
        {
            DisplayName = "FrontDoor",
            Path = "unmapped",
        };
        await thumbnailPublisher.PublishThumbnail(
            CameraClipRequest.FromDoorBird(@event, source),
            cancellationToken);
    }
}
