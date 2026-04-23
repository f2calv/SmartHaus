namespace CasCap.Services;

/// <summary>
/// In-memory event sink that tracks <see cref="DoorBirdEvent"/> counts and timestamps
/// per <see cref="DoorBirdEventType"/> and provides snapshot queries without requiring external infrastructure.
/// </summary>
[SinkType("Memory")]
public class DoorBirdSinkMemoryService(ILogger<DoorBirdSinkMemoryService> logger) : IEventSink<DoorBirdEvent>, IDoorBirdQuery
{
    private readonly ConcurrentDictionary<DoorBirdEventType, (DateTime LastUtc, int Count)> _state = new();

    /// <inheritdoc/>
    public Task WriteEvent(DoorBirdEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@DoorBirdEvent}", nameof(DoorBirdSinkMemoryService), @event);
        _state.AddOrUpdate(
            @event.DoorBirdEventType,
            (@event.DateCreatedUtc, 1),
            (_, existing) => (@event.DateCreatedUtc, existing.Count + 1));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<DoorBirdSnapshot> GetSnapshot()
        => Task.FromResult(new DoorBirdSnapshot
        {
            SnapshotUtc = DateTime.UtcNow,
            LastDoorbellUtc = GetLastUtc(DoorBirdEventType.Doorbell),
            LastMotionUtc = GetLastUtc(DoorBirdEventType.MotionSensor),
            LastRfidUtc = GetLastUtc(DoorBirdEventType.Rfid),
            LastRelayTriggerUtc = GetLastUtc(DoorBirdEventType.DoorRelay),
            DoorbellCount = GetCount(DoorBirdEventType.Doorbell),
            MotionCount = GetCount(DoorBirdEventType.MotionSensor),
            RfidCount = GetCount(DoorBirdEventType.Rfid),
            RelayTriggerCount = GetCount(DoorBirdEventType.DoorRelay),
        });

    /// <inheritdoc/>
    public IAsyncEnumerable<DoorBirdEvent> GetEvents(string? id = null, int limit = 1000,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    #region private helpers

    private DateTime? GetLastUtc(DoorBirdEventType eventType)
        => _state.TryGetValue(eventType, out var s) ? s.LastUtc : null;

    private int GetCount(DoorBirdEventType eventType)
        => _state.TryGetValue(eventType, out var s) ? s.Count : 0;

    #endregion
}
