namespace CasCap.Services;

/// <inheritdoc/>
[SinkType("Console")]
public class BuderusSinkConsoleService(ILogger<BuderusSinkConsoleService> logger) : IEventSink<BuderusEvent>
{
    /// <inheritdoc/>
    public Task WriteEvent(BuderusEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} Id={DatapointId}, Value={Value}, Type={GaugeType}",
            nameof(BuderusSinkConsoleService), @event.Id, @event.Value, @event.type);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    IAsyncEnumerable<BuderusEvent> IEventSink<BuderusEvent>.GetEvents(string? id, int limit, CancellationToken cancellationToken) => throw new NotImplementedException();
}
