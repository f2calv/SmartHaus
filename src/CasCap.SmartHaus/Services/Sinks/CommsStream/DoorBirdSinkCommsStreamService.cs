namespace CasCap.Services;

/// <summary>
/// Writes non-image <see cref="DoorBirdEvent"/> notifications (door relay activations)
/// directly to the comms Redis Stream. Image-bearing events (doorbell, motion, RFID)
/// are handled by <see cref="DoorBirdSinkMediaStreamService"/> which routes through
/// AI vision analysis before writing to the comms stream.
/// </summary>
[SinkType("CommsStream")]
public class DoorBirdSinkCommsStreamService(ILogger<DoorBirdSinkCommsStreamService> logger,
    IEventSink<CommsEvent> commsSink) : IEventSink<DoorBirdEvent>
{
    /// <inheritdoc/>
    public async Task WriteEvent(DoorBirdEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@DoorBirdEvent}", nameof(DoorBirdSinkCommsStreamService), @event);

        // Image events go through MediaStream → AI vision analysis → CommsStream; skip here to avoid duplicates.
        if (@event.bytes is not null)
            return;

        logger.LogInformation("{ClassName} relay event, writing to comms stream", nameof(DoorBirdSinkCommsStreamService));
        await commsSink.WriteEvent(new CommsEvent
        {
            Source = nameof(DoorBirdSinkCommsStreamService),
            Message = $"Front door entry relay activated at {@event.DateCreatedUtc:HH:mm:ss} UTC",
            TimestampUtc = @event.DateCreatedUtc,
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<DoorBirdEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
