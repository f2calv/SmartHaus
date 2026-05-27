namespace CasCap.Services;

/// <inheritdoc/>
[SinkType("Console")]
public sealed class BuderusSinkConsoleService(ILogger<BuderusSinkConsoleService> logger) : IEventSink<BuderusEvent>
{
    /// <inheritdoc/>
    public string SinkType => "Console";

    /// <inheritdoc/>
    public Task WriteEvent(BuderusEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} Id={DatapointId}, Value={Value}, Type={GaugeType}",
            nameof(BuderusSinkConsoleService), @event.Id, @event.Value, @event.type);
        return Task.CompletedTask;
    }

}
