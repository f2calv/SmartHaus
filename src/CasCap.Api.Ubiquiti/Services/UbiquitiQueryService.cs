namespace CasCap.Services;

/// <inheritdoc/>
public sealed class UbiquitiQueryService(
    ILogger<UbiquitiQueryService> logger,
    TimeProvider timeProvider,
    IEnumerable<IEventSink<UbiquitiEvent>> eventSinks,
    IUbiquitiQuery? ubiquitiQuery = null
    ) : IUbiquitiQueryService
{
    /// <inheritdoc/>
    public async Task<UbiquitiSnapshot> GetSnapshot()
    {
        if (ubiquitiQuery is null)
            return new UbiquitiSnapshot { SnapshotUtc = timeProvider.GetUtcNow().UtcDateTime };

        return await ubiquitiQuery.GetSnapshot();
    }

    /// <inheritdoc/>
    public async Task SendAlert(UbiquitiEventType type, string? cameraId = null, string? cameraName = null, double? score = null)
    {
        logger.LogInformation("{ClassName} sending alert for event type {EventType} from camera {CameraName}",
            nameof(UbiquitiQueryService), type, cameraName ?? cameraId ?? "unknown");

        var ubiquitiEvent = new UbiquitiEvent
        {
            UbiquitiEventType = type,
            DateCreatedUtc = timeProvider.GetUtcNow().UtcDateTime,
            CameraId = cameraId,
            CameraName = cameraName,
            Score = score,
        };

        var sinkTasks = new List<Task>(eventSinks.Count());
        foreach (var eventSink in eventSinks)
            sinkTasks.Add(eventSink.WriteEvent(ubiquitiEvent));
        await Task.WhenAll(sinkTasks.ToArray());
    }
}
