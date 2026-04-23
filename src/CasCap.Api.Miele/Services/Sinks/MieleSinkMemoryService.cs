using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace CasCap.Services;

/// <summary>
/// In-memory event sink that stores the latest <see cref="MieleEvent"/> per appliance and provides
/// snapshot queries without requiring external infrastructure.
/// </summary>
[SinkType("Memory")]
public class MieleSinkMemoryService(ILogger<MieleSinkMemoryService> logger) : IEventSink<MieleEvent>, IMieleQuery
{
    private readonly ConcurrentDictionary<string, MieleEvent> _latestByDevice = [];

    /// <inheritdoc/>
    public Task WriteEvent(MieleEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@MieleEvent}", nameof(MieleSinkMemoryService), @event);
        _latestByDevice[@event.DeviceId] = @event;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<List<MieleSnapshot>> GetSnapshots()
    {
        var snapshots = _latestByDevice.Values.Select(e => new MieleSnapshot
        {
            DeviceId = e.DeviceId,
            DeviceName = e.DeviceName,
            StatusCode = e.StatusCode,
            ProgramId = e.ProgramId,
            ErrorCode = e.ErrorCode,
            ReadingUtc = new DateTimeOffset(e.TimestampUtc, TimeSpan.Zero),
        }).ToList();
        return Task.FromResult(snapshots);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<MieleEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        foreach (var evt in _latestByDevice.Values.Take(limit))
            yield return evt;
    }
}
