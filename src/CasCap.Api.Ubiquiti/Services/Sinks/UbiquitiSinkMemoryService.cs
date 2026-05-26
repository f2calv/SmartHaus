namespace CasCap.Services;

/// <summary>
/// In-memory event sink that tracks <see cref="UbiquitiEvent"/> counts and timestamps
/// per <see cref="UbiquitiEventType"/> and provides snapshot queries without requiring external infrastructure.
/// </summary>
[SinkType("Memory")]
public partial class UbiquitiSinkMemoryService(ILogger<UbiquitiSinkMemoryService> logger, TimeProvider timeProvider) : IEventSink<UbiquitiEvent>, IUbiquitiQuery
{
    private readonly ConcurrentDictionary<UbiquitiEventType, (DateTime LastUtc, int Count)> _state = new();

    /// <inheritdoc/>
    public Task WriteEvent(UbiquitiEvent @event, CancellationToken cancellationToken = default)
    {
        LogWriteEvent(logger, nameof(UbiquitiSinkMemoryService), @event.UbiquitiEventType.ToString());
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
            SnapshotUtc = timeProvider.GetUtcNow().UtcDateTime,
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

    [LoggerMessage(Level = LogLevel.Trace, Message = "{ClassName} processing event of type {EventType}")]
    private static partial void LogWriteEvent(ILogger logger, string className, string eventType);
}
