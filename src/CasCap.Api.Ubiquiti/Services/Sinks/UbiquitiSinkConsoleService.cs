namespace CasCap.Services;

/// <inheritdoc/>
[SinkType("Console")]
public sealed class UbiquitiSinkConsoleService(ILogger<UbiquitiSinkConsoleService> logger) : IEventSink<UbiquitiEvent>
{
    /// <inheritdoc/>
    public string SinkType => "Console";

    /// <inheritdoc/>
    public Task WriteEvent(UbiquitiEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} EventType={EventType}, Camera={CameraName}, Score={Score}",
            nameof(UbiquitiSinkConsoleService), @event.UbiquitiEventType, @event.CameraName ?? @event.CameraId, @event.Score);
        return Task.CompletedTask;
    }

}
