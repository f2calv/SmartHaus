namespace CasCap.Services;

/// <inheritdoc/>
[SinkType("Console")]
public class WizSinkConsoleService(ILogger<WizSinkConsoleService> logger) : IEventSink<WizEvent>
{
    /// <inheritdoc/>
    public Task WriteEvent(WizEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} [{DeviceId}] State: {State}, Dimming: {Dimming}, Scene: {SceneId}, Temp: {Temp}K, RSSI: {Rssi}dBm",
            nameof(WizSinkConsoleService),
            @event.DeviceId, @event.State, @event.Dimming, @event.SceneId, @event.Temp, @event.Rssi);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<WizEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
