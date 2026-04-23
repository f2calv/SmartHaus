namespace CasCap.Services;

/// <inheritdoc/>
[SinkType("Console")]
public class DoorBirdSinkConsoleService(ILogger<DoorBirdSinkConsoleService> logger) : IEventSink<DoorBirdEvent>
{
    /// <inheritdoc/>
    public Task WriteEvent(DoorBirdEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} EventType={EventType}, HasImage={HasImage}",
            nameof(DoorBirdSinkConsoleService), @event.DoorBirdEventType, @event.bytes is not null);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<DoorBirdEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
