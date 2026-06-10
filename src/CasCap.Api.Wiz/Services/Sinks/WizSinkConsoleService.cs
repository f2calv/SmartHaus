namespace CasCap.Services;

/// <inheritdoc/>
[SinkType("Console")]
public sealed class WizSinkConsoleService(ILogger<WizSinkConsoleService> logger) : IEventSink<WizEvent>
{
    /// <inheritdoc/>
    public string SinkType => "Console";

    /// <inheritdoc/>
    public Task WriteEvent(WizEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} [{DeviceId}] State: {State}, Dimming: {Dimming}, Scene: {SceneId}, Temp: {Temp}K, RSSI: {Rssi}dBm",
            nameof(WizSinkConsoleService),
            @event.DeviceId, @event.State, @event.Dimming, @event.SceneId, @event.Temp, @event.Rssi);
        return Task.CompletedTask;
    }

}
