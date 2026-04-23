namespace CasCap.Services;

/// <summary>
/// Monitors Sicce pump events for critical state changes (offline, power switch toggle)
/// and writes alerts to the comms Redis Stream via <see cref="IEventSink{T}"/>.
/// </summary>
[SinkType("CommsStream")]
public class SicceSinkCommsStreamService(ILogger<SicceSinkCommsStreamService> logger,
    IEventSink<CommsEvent> commsSink) : IEventSink<SicceEvent>
{
    private bool? _lastIsOnline;
    private bool? _lastPowerSwitch;

    /// <inheritdoc/>
    public async Task WriteEvent(SicceEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@SicceEvent}", nameof(SicceSinkCommsStreamService), @event);

        if (_lastIsOnline is not null && _lastIsOnline.Value && !@event.IsOnline)
        {
            logger.LogInformation("{ClassName} pump offline alert", nameof(SicceSinkCommsStreamService));
            await commsSink.WriteEvent(new CommsEvent
            {
                Source = nameof(SicceSinkCommsStreamService),
                Message = "Aquarium pump has gone offline",
                TimestampUtc = @event.TimestampUtc,
            }, cancellationToken);
        }
        else if (_lastIsOnline is not null && !_lastIsOnline.Value && @event.IsOnline)
        {
            logger.LogInformation("{ClassName} pump back online", nameof(SicceSinkCommsStreamService));
            await commsSink.WriteEvent(new CommsEvent
            {
                Source = nameof(SicceSinkCommsStreamService),
                Message = "Aquarium pump is back online",
                TimestampUtc = @event.TimestampUtc,
            }, cancellationToken);
        }

        if (_lastPowerSwitch is not null && _lastPowerSwitch.Value != @event.PowerSwitch)
        {
            var state = @event.PowerSwitch ? "on" : "off";
            logger.LogInformation("{ClassName} power switch toggled {State}", nameof(SicceSinkCommsStreamService), state);
            await commsSink.WriteEvent(new CommsEvent
            {
                Source = nameof(SicceSinkCommsStreamService),
                Message = $"Aquarium pump power switch toggled {state}",
                TimestampUtc = @event.TimestampUtc,
            }, cancellationToken);
        }

        _lastIsOnline = @event.IsOnline;
        _lastPowerSwitch = @event.PowerSwitch;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<SicceEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
