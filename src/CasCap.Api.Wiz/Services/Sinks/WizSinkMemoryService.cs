using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace CasCap.Services;

/// <summary>
/// In-memory event sink that stores the latest <see cref="WizEvent"/> per bulb and provides
/// snapshot queries without requiring external infrastructure.
/// </summary>
[SinkType("Memory")]
public class WizSinkMemoryService(ILogger<WizSinkMemoryService> logger) : IEventSink<WizEvent>, IWizQuery
{
    private readonly ConcurrentDictionary<string, WizEvent> _latestByBulb = [];

    /// <inheritdoc/>
    public Task WriteEvent(WizEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@WizEvent}", nameof(WizSinkMemoryService), @event);
        _latestByBulb[@event.DeviceId] = @event;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<List<WizSnapshot>> GetSnapshots()
    {
        var snapshots = _latestByBulb.Values.Select(e => new WizSnapshot
        {
            DeviceId = e.DeviceId,
            IpAddress = e.IpAddress,
            Mac = e.Mac,
            State = e.State,
            Dimming = e.Dimming,
            SceneId = e.SceneId,
            Temp = e.Temp,
            Rssi = e.Rssi,
            ReadingUtc = new DateTimeOffset(e.TimestampUtc, TimeSpan.Zero),
        }).ToList();
        return Task.FromResult(snapshots);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WizEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        foreach (var evt in _latestByBulb.Values.Take(limit))
            yield return evt;
    }
}
