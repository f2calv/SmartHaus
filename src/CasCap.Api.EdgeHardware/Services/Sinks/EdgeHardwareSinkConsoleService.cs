namespace CasCap.Services;

/// <summary>
/// Event sink that logs <see cref="EdgeHardwareEvent"/> data points at debug level.
/// Useful for local development and diagnostics.
/// </summary>
[SinkType("Console")]
public class EdgeHardwareSinkConsoleService(ILogger<EdgeHardwareSinkConsoleService> logger) : IEventSink<EdgeHardwareEvent>
{
    /// <inheritdoc/>
    public Task WriteEvent(EdgeHardwareEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "{ClassName} Node={NodeName} GPU: {GpuPowerDrawW}W, {GpuTemperatureC}°C, util {GpuUtilizationPercent}%, mem {GpuMemoryUsedMiB}/{GpuMemoryTotalMiB} MiB ({GpuMemoryUtilizationPercent}%) CPU: {CpuTemperatureC}°C",
            nameof(EdgeHardwareSinkConsoleService),
            @event.NodeName,
            @event.GpuPowerDrawW, @event.GpuTemperatureC, @event.GpuUtilizationPercent,
            @event.GpuMemoryUsedMiB, @event.GpuMemoryTotalMiB, @event.GpuMemoryUtilizationPercent,
            @event.CpuTemperatureC);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<EdgeHardwareEvent> GetEvents(string? id = null, int limit = 1000,
        CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
