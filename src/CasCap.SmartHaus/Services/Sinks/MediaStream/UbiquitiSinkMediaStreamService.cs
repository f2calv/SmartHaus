namespace CasCap.Services;

/// <summary>Routes UniFi Protect thumbnail events through the security media-analysis pipeline.</summary>
[SinkType("MediaStream")]
public sealed class UbiquitiSinkMediaStreamService(
    ILogger<UbiquitiSinkMediaStreamService> logger,
    CameraClipQueue clipQueue,
    CameraThumbnailPublisher mediaPublisher) : IEventSink<UbiquitiEvent>
{
    /// <inheritdoc/>
    public string SinkType => "MediaStream";

    /// <inheritdoc/>
    public async Task WriteEvent(UbiquitiEvent @event, CancellationToken cancellationToken = default)
    {
        var admission = clipQueue.TryEnqueue(@event);
        if (admission is CameraClipAdmission.Enqueued)
        {
            logger.LogInformation("{ClassName} queued {EventType} clip capture",
                nameof(UbiquitiSinkMediaStreamService), @event.UbiquitiEventType);
            return;
        }

        if (admission is CameraClipAdmission.Suppressed)
        {
            logger.LogDebug("{ClassName} suppressed a repeated {EventType} clip event",
                nameof(UbiquitiSinkMediaStreamService), @event.UbiquitiEventType);
            return;
        }

        if (admission is CameraClipAdmission.QueueFull)
        {
            logger.LogWarning("{ClassName} clip queue is full; publishing thumbnail fallback",
                nameof(UbiquitiSinkMediaStreamService));
        }

        var source = new CameraClipSourceConfig
        {
            DisplayName = @event.CameraName ?? @event.CameraId ?? "Camera",
            Path = "unmapped",
        };
        await mediaPublisher.PublishThumbnail(
            CameraClipRequest.FromUbiquiti(@event, source),
            cancellationToken);
    }
}
