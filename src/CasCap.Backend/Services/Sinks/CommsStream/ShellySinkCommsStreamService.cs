using System.Collections.Concurrent;

namespace CasCap.Services;

/// <summary>
/// Writes a <see cref="CommsEvent"/> to the comms Redis Stream when a smart plug
/// reports an overpower condition. Tracks alert state per device.
/// </summary>
[SinkType("CommsStream")]
public sealed class ShellySinkCommsStreamService(ILogger<ShellySinkCommsStreamService> logger, IHostEnvironment env, IEventSink<CommsEvent> commsSink) : IEventSink<ShellyEvent>
{
    /// <inheritdoc/>
    public string SinkType => "CommsStream";

    private readonly ConcurrentDictionary<string, bool> _alertFiredByDevice = [];

    /// <inheritdoc/>
    public async Task WriteEvent(ShellyEvent @event, CancellationToken cancellationToken = default)
    {
        _alertFiredByDevice.TryGetValue(@event.DeviceId, out var alertFired);

        if (@event.Overpower >= 1)
        {
            if (!alertFired)
            {
                _alertFiredByDevice[@event.DeviceId] = true;
                var commsEvent = new CommsEvent
                {
                    Source = nameof(ShellySinkCommsStreamService),
                    Message = $"Smart plug '{@event.DeviceName}' overpower alert — Power={@event.Power:F0}W, Temperature={@event.Temperature:F1}°C",
                    Environment = env.GetAcronym(),
                    TimestampUtc = @event.TimestampUtc,
                };
                await commsSink.WriteEvent(commsEvent, cancellationToken);
                logger.LogInformation("{ClassName} overpower alert fired for {DeviceId} ({DeviceName}) at {Power}W",
                    nameof(ShellySinkCommsStreamService), @event.DeviceId, @event.DeviceName, @event.Power);
            }
        }
        else
            _alertFiredByDevice[@event.DeviceId] = false;
    }

}
