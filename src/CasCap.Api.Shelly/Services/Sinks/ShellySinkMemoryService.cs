namespace CasCap.Services;

/// <summary>
/// In-memory event sink that stores the latest <see cref="ShellyEvent"/> per device and provides
/// snapshot queries without requiring external infrastructure.
/// </summary>
[SinkType("Memory")]
public class ShellySinkMemoryService(ILogger<ShellySinkMemoryService> logger) : IEventSink<ShellyEvent>, IShellyQuery
{
    private readonly Dictionary<string, ShellyEvent> _latestByDevice = [];

    /// <inheritdoc/>
    public Task WriteEvent(ShellyEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@ShellyEvent}", nameof(ShellySinkMemoryService), @event);
        _latestByDevice[@event.DeviceId] = @event;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<List<ShellySnapshot>> GetSnapshots()
    {
        var snapshots = _latestByDevice.Values.Select(e => new ShellySnapshot
        {
            DeviceId = e.DeviceId,
            DeviceName = e.DeviceName,
            Power = e.Power,
            IsOn = e.RelayState >= 1,
            Temperature = e.Temperature,
            Overpower = e.Overpower >= 1,
            ReadingUtc = new DateTimeOffset(e.TimestampUtc, TimeSpan.Zero),
        }).ToList();
        return Task.FromResult(snapshots);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ShellyEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        foreach (var e in _latestByDevice.Values)
            yield return e;
    }
}
