namespace CasCap.Services;

/// <summary>
/// Writes <see cref="UbiquitiEvent"/> metadata to the comms Redis Stream via
/// <see cref="IEventSink{T}"/> for downstream processing by <see cref="CommunicationsBgService"/>.
/// </summary>
[SinkType("CommsStream")]
public sealed class UbiquitiSinkCommsStreamService(ILogger<UbiquitiSinkCommsStreamService> logger,
    IHostEnvironment env,
    CameraClipQueue clipQueue,
    IEventSink<CommsEvent> commsSink) : IEventSink<UbiquitiEvent>
{
    /// <inheritdoc/>
    public string SinkType => "CommsStream";


    /// <inheritdoc/>
    public async Task WriteEvent(UbiquitiEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "{ClassName} processing EventType={EventType}, Camera={Camera}, Score={Score}, ThumbnailBytes={ThumbnailBytes}",
            nameof(UbiquitiSinkCommsStreamService),
            @event.UbiquitiEventType,
            @event.CameraName ?? @event.CameraId,
            @event.Score,
            @event.Thumbnail?.Length ?? 0);

        if (@event.Thumbnail is not null || clipQueue.IsConfigured(@event.SourceCameraId))
            return;

        var commsEvent = new CommsEvent
        {
            Source = nameof(UbiquitiSinkCommsStreamService),
            Message = $"Security camera {@event.CameraName ?? @event.CameraId ?? "unknown"} detected {@event.UbiquitiEventType} at {@event.DateCreatedUtc:yyyy-MM-dd HH:mm:ss} UTC",
            Environment = env.GetAcronym(),
            TimestampUtc = @event.DateCreatedUtc,
            JsonPayload = @event.ToJson(),
        };

        logger.LogInformation("{ClassName} event detected {UbiquitiEvent}, writing to comms stream",
            nameof(UbiquitiSinkCommsStreamService), @event);
        await commsSink.WriteEvent(commsEvent, cancellationToken);
    }
}
