namespace CasCap.Services.Sinks;

/// <summary>
/// Event sink that broadcasts each <see cref="DoorBirdEvent"/> to all connected
/// <see cref="HausHub"/> clients via <see cref="IHubContext{THub,T}"/>.
/// Image bytes are stripped before broadcast to avoid large WebSocket payloads.
/// Enable via <c>"SignalR": { "Enabled": true }</c> in the DoorBirdConfig Sinks section.
/// </summary>
[SinkType("SignalR")]
public class HausHubSinkDoorBirdService(
    ILogger<HausHubSinkDoorBirdService> logger,
    IHubContext<HausHub, IHausClientHub> hubContext) : IEventSink<DoorBirdEvent>
{
    /// <inheritdoc />
    public async Task WriteEvent(DoorBirdEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} broadcasting DoorBird event {EventType}", nameof(HausHubSinkDoorBirdService), @event.DoorBirdEventType);
        // Strip image bytes before broadcasting — keep the payload lightweight.
        var slim = @event with { bytes = null };
        await hubContext.Clients.All.ReceiveDoorBirdEvent(slim);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<DoorBirdEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
