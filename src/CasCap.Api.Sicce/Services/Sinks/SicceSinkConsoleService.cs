namespace CasCap.Services;

/// <inheritdoc/>
[SinkType("Console")]
public class SicceSinkConsoleService(ILogger<SicceSinkConsoleService> logger) : IEventSink<SicceEvent>
{
    /// <inheritdoc/>
    public Task WriteEvent(SicceEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} Temp: {Temperature}°C, Power: {Power:P0}, Online: {IsOnline}, Switch: {PowerSwitch}",
            nameof(SicceSinkConsoleService),
            @event.Temperature, @event.Power, @event.IsOnline, @event.PowerSwitch);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<SicceEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
