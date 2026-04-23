namespace CasCap.Services.Sinks;

/// <summary>
/// Event sink that broadcasts each <see cref="KnxEvent"/> to all connected
/// <see cref="HausHub"/> clients via <see cref="IHubContext{THub,T}"/>.
/// Enable via <c>"SignalR": { "Enabled": true }</c> in the KnxConfig Sinks section.
/// </summary>
[SinkType("SignalR")]
public class HausHubSinkKnxService(
    ILogger<HausHubSinkKnxService> logger,
    IHubContext<HausHub, IHausClientHub> hubContext) : IEventSink<KnxEvent>
{
    /// <inheritdoc />
    public async Task WriteEvent(KnxEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} broadcasting {GroupAddress}", nameof(HausHubSinkKnxService), @event.Kga?.GroupAddress);
        await hubContext.Clients.All.ReceiveKnxEvent(@event);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<KnxEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
