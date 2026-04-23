namespace CasCap.Services;

/// <inheritdoc/>
[SinkType("Console")]
public class MieleSinkConsoleService(ILogger<MieleSinkConsoleService> logger) : IEventSink<MieleEvent>
{
    /// <inheritdoc/>
    public Task WriteEvent(MieleEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} [{DeviceId}] {DeviceName} {EventType}, Status: {StatusCode}, Program: {ProgramName}, Error: {ErrorCode}",
            nameof(MieleSinkConsoleService),
            @event.DeviceId, @event.DeviceName, @event.EventType, @event.StatusCode, @event.ProgramName, @event.ErrorCode);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<MieleEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
