namespace CasCap.Services;

/// <summary>
/// In-memory event sink that stores the latest <see cref="EdgeHardwareEvent"/> for
/// snapshot queries without requiring external infrastructure.
/// </summary>
[SinkType("Memory")]
public class EdgeHardwareSinkMemoryService(ILogger<EdgeHardwareSinkMemoryService> logger) : IEventSink<EdgeHardwareEvent>, IEdgeHardwareQuery
{
    private EdgeHardwareEvent? _latest;

    /// <inheritdoc/>
    public Task WriteEvent(EdgeHardwareEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@Data}", nameof(EdgeHardwareSinkMemoryService), @event);
        _latest = @event;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<List<EdgeHardwareSnapshot>> GetSnapshots()
    {
        if (_latest is null)
            return Task.FromResult<List<EdgeHardwareSnapshot>>([]);

        return Task.FromResult<List<EdgeHardwareSnapshot>>(
        [
            new EdgeHardwareSnapshot
            {
                NodeName = _latest.NodeName,
                GpuPowerDrawW = _latest.GpuPowerDrawW,
                GpuTemperatureC = _latest.GpuTemperatureC,
                GpuUtilizationPercent = _latest.GpuUtilizationPercent,
                GpuMemoryUtilizationPercent = _latest.GpuMemoryUtilizationPercent,
                GpuMemoryUsedMiB = _latest.GpuMemoryUsedMiB,
                GpuMemoryTotalMiB = _latest.GpuMemoryTotalMiB,
                CpuTemperatureC = _latest.CpuTemperatureC,
                Timestamp = _latest.TimestampUtc,
            }
        ]);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<EdgeHardwareEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        if (_latest is not null)
            yield return _latest;
    }
}
