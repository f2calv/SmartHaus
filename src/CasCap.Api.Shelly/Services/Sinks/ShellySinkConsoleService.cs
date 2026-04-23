namespace CasCap.Services;

/// <inheritdoc/>
[SinkType("Console")]
public class ShellySinkConsoleService(ILogger<ShellySinkConsoleService> logger) : IEventSink<ShellyEvent>
{
    /// <inheritdoc/>
    public Task WriteEvent(ShellyEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} [{DeviceId}] {DeviceName} Power: {Power}W, Relay: {RelayState}, Temp: {Temperature}°C",
            nameof(ShellySinkConsoleService),
            @event.DeviceId, @event.DeviceName, @event.Power, @event.RelayState, @event.Temperature);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<ShellyEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
