namespace CasCap.Services;

/// <summary>
/// Writes <see cref="UbiquitiEvent"/> metadata to the comms Redis Stream via
/// <see cref="IEventSink{T}"/> for downstream processing by <see cref="CommunicationsBgService"/>.
/// </summary>
[SinkType("CommsStream")]
public class UbiquitiSinkCommsStreamService(ILogger<UbiquitiSinkCommsStreamService> logger,
    IEventSink<CommsEvent> commsSink) : IEventSink<UbiquitiEvent>
{
    /// <inheritdoc/>
    public IAsyncEnumerable<UbiquitiEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public async Task WriteEvent(UbiquitiEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} {@UbiquitiEvent}", nameof(UbiquitiSinkCommsStreamService), @event);

        var commsEvent = new CommsEvent
        {
            Source = nameof(UbiquitiSinkCommsStreamService),
            Message = $"Security camera {@event.CameraName ?? @event.CameraId ?? "unknown"} detected {@event.UbiquitiEventType} at {@event.DateCreatedUtc:yyyy-MM-dd HH:mm:ss} UTC",
            TimestampUtc = @event.DateCreatedUtc,
            JsonPayload = @event.ToJson(),
        };

        logger.LogInformation("{ClassName} event detected {UbiquitiEvent}, writing to comms stream",
            nameof(UbiquitiSinkCommsStreamService), @event);
        await commsSink.WriteEvent(commsEvent, cancellationToken);
    }
}
