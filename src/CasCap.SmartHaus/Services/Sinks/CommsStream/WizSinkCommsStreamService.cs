using System.Collections.Concurrent;

namespace CasCap.Services;

/// <summary>
/// Monitors Wiz bulb events for on/off state changes and writes alerts
/// to the comms Redis Stream via <see cref="IEventSink{T}"/>.
/// </summary>
[SinkType("CommsStream")]
public class WizSinkCommsStreamService(ILogger<WizSinkCommsStreamService> logger,
    IEventSink<CommsEvent> commsSink) : IEventSink<WizEvent>
{
    private readonly ConcurrentDictionary<string, bool> _previousStates = [];

    /// <inheritdoc/>
    public async Task WriteEvent(WizEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@WizEvent}", nameof(WizSinkCommsStreamService), @event);

        _previousStates.TryGetValue(@event.DeviceId, out var previous);
        var isFirstSeen = !_previousStates.ContainsKey(@event.DeviceId);
        _previousStates[@event.DeviceId] = @event.State;

        // Only alert on state changes, not first-seen discoveries
        if (isFirstSeen)
            return;

        if (previous != @event.State)
        {
            var action = @event.State ? "on" : "off";
            var bulbId = @event.Mac ?? @event.IpAddress;
            logger.LogInformation("{ClassName} bulb {BulbId} turned {Action}", nameof(WizSinkCommsStreamService), bulbId, action);
            await commsSink.WriteEvent(new CommsEvent
            {
                Source = nameof(WizSinkCommsStreamService),
                Message = $"Smart light {bulbId} turned {action}",
                TimestampUtc = @event.TimestampUtc,
            }, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<WizEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
