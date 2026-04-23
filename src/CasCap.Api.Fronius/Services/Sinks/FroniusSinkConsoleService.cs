namespace CasCap.Services;

/// <inheritdoc/>
[SinkType("Console")]
public class FroniusSinkConsoleService(ILogger<FroniusSinkConsoleService> logger) : IEventSink<FroniusEvent>
{
    /// <inheritdoc/>
    public Task WriteEvent(FroniusEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} PV: {PvW}W, Grid: {GridW}W, Load: {LoadW}W, Battery: {AkkuW}W, SOC: {Soc:P0}",
            nameof(FroniusSinkConsoleService),
            @event.P_PV, @event.P_Grid, @event.P_Load, @event.P_Akku, @event.SOC);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<FroniusEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
