namespace CasCap.Services;

/// <summary>
/// Monitors Miele appliance events for program completion and errors,
/// and writes alerts to the comms Redis Stream via <see cref="IEventSink{T}"/>.
/// </summary>
[SinkType("CommsStream")]
public class MieleSinkCommsStreamService(ILogger<MieleSinkCommsStreamService> logger,
    IEventSink<CommsEvent> commsSink) : IEventSink<MieleEvent>
{
    /// <inheritdoc/>
    public async Task WriteEvent(MieleEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@MieleEvent}", nameof(MieleSinkCommsStreamService), @event);

        if (@event.EventType is MieleEventType.Error && @event.ErrorCode is not null and not 0)
        {
            logger.LogInformation("{ClassName} appliance error on {DeviceId}: code {ErrorCode}",
                nameof(MieleSinkCommsStreamService), @event.DeviceId, @event.ErrorCode);
            await commsSink.WriteEvent(new CommsEvent
            {
                Source = nameof(MieleSinkCommsStreamService),
                Message = $"Appliance {@event.DeviceName ?? @event.DeviceId} reported error code {@event.ErrorCode}",
                TimestampUtc = @event.TimestampUtc,
            }, cancellationToken);
        }

        if (@event.EventType is MieleEventType.ProgramComplete)
        {
            logger.LogInformation("{ClassName} program complete on {DeviceId}: {ProgramName}",
                nameof(MieleSinkCommsStreamService), @event.DeviceId, @event.ProgramName);
            await commsSink.WriteEvent(new CommsEvent
            {
                Source = nameof(MieleSinkCommsStreamService),
                Message = $"Appliance {@event.DeviceName ?? @event.DeviceId} program '{@event.ProgramName ?? $"#{@event.ProgramId}"}' completed",
                TimestampUtc = @event.TimestampUtc,
            }, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<MieleEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
