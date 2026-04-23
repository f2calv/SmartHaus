namespace CasCap.Services;

/// <summary>
/// In-memory event sink that stores the latest <see cref="SicceEvent"/> and provides
/// snapshot queries without requiring external infrastructure.
/// </summary>
[SinkType("Memory")]
public class SicceSinkMemoryService(ILogger<SicceSinkMemoryService> logger) : IEventSink<SicceEvent>, ISicceQuery
{
    private SicceEvent? _latest;

    /// <inheritdoc/>
    public Task WriteEvent(SicceEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@SicceEvent}", nameof(SicceSinkMemoryService), @event);
        _latest = @event;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<SicceSnapshot> GetSnapshot()
    {
        if (_latest is null)
            return Task.FromResult(new SicceSnapshot());

        return Task.FromResult(new SicceSnapshot
        {
            Temperature = _latest.Temperature,
            Power = _latest.Power,
            IsOnline = _latest.IsOnline,
            PowerSwitch = _latest.PowerSwitch,
            ReadingUtc = new DateTimeOffset(_latest.TimestampUtc, TimeSpan.Zero),
        });
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<SicceEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        if (_latest is not null)
            yield return _latest;
    }
}
