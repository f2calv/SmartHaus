namespace CasCap.Services;

/// <summary>
/// In-memory event sink that stores the latest <see cref="SicceEvent"/> and provides
/// snapshot queries without requiring external infrastructure.
/// </summary>
[SinkType("Memory")]
public partial class SicceSinkMemoryService(ILogger<SicceSinkMemoryService> logger) : IEventSink<SicceEvent>, ISicceQuery
{
    /// <inheritdoc/>
    public string SinkType => "Memory";

    private SicceEvent? _latest;

    /// <inheritdoc/>
    public Task WriteEvent(SicceEvent @event, CancellationToken cancellationToken = default)
    {
        LogWriteEvent(logger, nameof(SicceSinkMemoryService));
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

    [LoggerMessage(Level = LogLevel.Trace, Message = "{ClassName} processing pump event")]
    private static partial void LogWriteEvent(ILogger logger, string className);
}
