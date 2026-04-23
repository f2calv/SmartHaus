namespace CasCap.Services.Sinks;

/// <summary>
/// Event sink that broadcasts each <see cref="FroniusEvent"/> to all connected
/// <see cref="HausHub"/> clients via <see cref="IHubContext{THub,T}"/>.
/// Enable via <c>"SignalR": { "Enabled": true }</c> in the FroniusConfig Sinks section.
/// </summary>
[SinkType("SignalR")]
public class HausHubSinkFroniusService(
    ILogger<HausHubSinkFroniusService> logger,
    IHubContext<HausHub, IHausClientHub> hubContext) : IEventSink<FroniusEvent>
{
    /// <inheritdoc />
    public async Task WriteEvent(FroniusEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} broadcasting Fronius event", nameof(HausHubSinkFroniusService));
        await hubContext.Clients.All.ReceiveFroniusEvent(@event);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<FroniusEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
