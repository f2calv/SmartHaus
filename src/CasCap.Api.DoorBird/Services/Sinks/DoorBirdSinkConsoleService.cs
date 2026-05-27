namespace CasCap.Services;

/// <inheritdoc/>
[SinkType("Console")]
public class DoorBirdSinkConsoleService(ILogger<DoorBirdSinkConsoleService> logger) : IEventSink<DoorBirdEvent>
{
    /// <inheritdoc/>
    public string SinkType => "Console";

    /// <inheritdoc/>
    public Task WriteEvent(DoorBirdEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} EventType={EventType}, HasImage={HasImage}",
            nameof(DoorBirdSinkConsoleService), @event.DoorBirdEventType, @event.bytes is not null);
        return Task.CompletedTask;
    }

}
