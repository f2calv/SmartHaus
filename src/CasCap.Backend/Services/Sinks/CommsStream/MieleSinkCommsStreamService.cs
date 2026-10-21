namespace CasCap.Services;

/// <summary>
/// Monitors Miele appliance events for program completion and errors,
/// and writes alerts to the comms Redis Stream via <see cref="IEventSink{T}"/>.
/// </summary>
[SinkType("CommsStream")]
public sealed partial class MieleSinkCommsStreamService(ILogger<MieleSinkCommsStreamService> logger,
    IHostEnvironment env,
    IEventSink<CommsEvent> commsSink) : IEventSink<MieleEvent>
{
    /// <inheritdoc/>
    public string SinkType => "CommsStream";

    /// <inheritdoc/>
    public async Task WriteEvent(MieleEvent @event, CancellationToken cancellationToken = default)
    {
        LogWriteEvent(logger, nameof(MieleSinkCommsStreamService), @event.DeviceId);

        if (@event.EventType is MieleEventType.Error && @event.ErrorCode is not null and not 0)
        {
            logger.LogInformation("{ClassName} appliance error on {DeviceId}: code {ErrorCode}",
                nameof(MieleSinkCommsStreamService), @event.DeviceId, @event.ErrorCode);
            await commsSink.WriteEvent(new CommsEvent
            {
                Source = nameof(MieleSinkCommsStreamService),
                Message = $"Appliance {@event.DeviceName ?? @event.DeviceId} reported error code {@event.ErrorCode}",
                Environment = env.GetAcronym(),
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
                Environment = env.GetAcronym(),
                TimestampUtc = @event.TimestampUtc,
            }, cancellationToken);
        }
    }


    [LoggerMessage(Level = LogLevel.Trace, Message = "{ClassName} processing event for device {DeviceId}")]
    private static partial void LogWriteEvent(ILogger logger, string className, string deviceId);
}
