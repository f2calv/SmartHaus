namespace CasCap.Services;

/// <summary>
/// In-memory event sink that tracks <see cref="UbiquitiEvent"/> counts and timestamps
/// per <see cref="UbiquitiEventType"/> and provides snapshot queries without requiring external infrastructure.
/// </summary>
[SinkType("Memory")]
public class UbiquitiSinkMemoryService(ILogger<UbiquitiSinkMemoryService> logger) : IEventSink<UbiquitiEvent>, IUbiquitiQuery
{
    private readonly ConcurrentDictionary<UbiquitiEventType, (DateTime LastUtc, int Count)> _state = new();

    /// <inheritdoc/>
    public Task WriteEvent(UbiquitiEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@UbiquitiEvent}", nameof(UbiquitiSinkMemoryService), @event);
        _state.AddOrUpdate(
            @event.UbiquitiEventType,
            (@event.DateCreatedUtc, 1),
            (_, existing) => (@event.DateCreatedUtc, existing.Count + 1));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<UbiquitiSnapshot> GetSnapshot()
        => Task.FromResult(new UbiquitiSnapshot
        {
            SnapshotUtc = DateTime.UtcNow,
            LastMotionUtc = GetLastUtc(UbiquitiEventType.Motion),
            LastSmartDetectPersonUtc = GetLastUtc(UbiquitiEventType.SmartDetectPerson),
            LastSmartDetectVehicleUtc = GetLastUtc(UbiquitiEventType.SmartDetectVehicle),
            LastSmartDetectAnimalUtc = GetLastUtc(UbiquitiEventType.SmartDetectAnimal),
            LastSmartDetectPackageUtc = GetLastUtc(UbiquitiEventType.SmartDetectPackage),
            LastRingUtc = GetLastUtc(UbiquitiEventType.Ring),
            MotionCount = GetCount(UbiquitiEventType.Motion),
            SmartDetectPersonCount = GetCount(UbiquitiEventType.SmartDetectPerson),
            SmartDetectVehicleCount = GetCount(UbiquitiEventType.SmartDetectVehicle),
            SmartDetectAnimalCount = GetCount(UbiquitiEventType.SmartDetectAnimal),
            SmartDetectPackageCount = GetCount(UbiquitiEventType.SmartDetectPackage),
            RingCount = GetCount(UbiquitiEventType.Ring),
        });

    /// <inheritdoc/>
    public IAsyncEnumerable<UbiquitiEvent> GetEvents(string? id = null, int limit = 1000,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    #region private helpers

    private DateTime? GetLastUtc(UbiquitiEventType eventType)
        => _state.TryGetValue(eventType, out var s) ? s.LastUtc : null;

    private int GetCount(UbiquitiEventType eventType)
        => _state.TryGetValue(eventType, out var s) ? s.Count : 0;

    #endregion
}
