namespace CasCap.Services.Sinks;

/// <summary>
/// Event sink that broadcasts each <see cref="BuderusEvent"/> to all connected
/// <see cref="HausHub"/> clients via <see cref="IHubContext{THub,T}"/>.
/// Enable via <c>"SignalR": { "Enabled": true }</c> in the BuderusConfig Sinks section.
/// </summary>
[SinkType("SignalR")]
public class HausHubSinkBuderusService(
    ILogger<HausHubSinkBuderusService> logger,
    IHubContext<HausHub, IHausClientHub> hubContext) : IEventSink<BuderusEvent>
{
    /// <inheritdoc />
    public async Task WriteEvent(BuderusEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} broadcasting Buderus event {EventId}", nameof(HausHubSinkBuderusService), @event.Id);
        await hubContext.Clients.All.ReceiveBuderusEvent(@event);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<BuderusEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
