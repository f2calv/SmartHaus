namespace CasCap.Services;

/// <summary>
/// In-memory event sink that stores <see cref="BuderusEvent"/> values in a <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// and provides snapshot queries without requiring external infrastructure.
/// </summary>
[SinkType("Memory")]
public class BuderusSinkMemoryService(ILogger<BuderusSinkMemoryService> logger, IOptions<BuderusConfig> config) : IEventSink<BuderusEvent>, IBuderusQuery
{
    private readonly ConcurrentDictionary<string, string> _values = new();

    /// <inheritdoc/>
    public Task WriteEvent(BuderusEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@BuderusEvent}", nameof(BuderusSinkMemoryService), @event);
        _values[@event.Id] = @event.Value;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<BuderusSnapshot> GetSnapshot()
    {
        var valuesByColumn = new Dictionary<string, string>();
        foreach (var (datapointId, value) in _values)
        {
            if (config.Value.DatapointMappings.TryGetValue(datapointId, out var mapping))
                valuesByColumn[mapping.ColumnName] = value;
        }
        return Task.FromResult(BuderusSnapshot.FromValues(valuesByColumn));
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<BuderusEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        if (id is null)
        {
            foreach (var (key, value) in _values)
                yield return new BuderusEvent(key, value, DateTime.UtcNow);
        }
    }
}
