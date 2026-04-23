namespace CasCap.Services;

/// <inheritdoc/>
[SinkType("Console")]
public class UbiquitiSinkConsoleService(ILogger<UbiquitiSinkConsoleService> logger) : IEventSink<UbiquitiEvent>
{
    /// <inheritdoc/>
    public Task WriteEvent(UbiquitiEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} EventType={EventType}, Camera={CameraName}, Score={Score}",
            nameof(UbiquitiSinkConsoleService), @event.UbiquitiEventType, @event.CameraName ?? @event.CameraId, @event.Score);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<UbiquitiEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
