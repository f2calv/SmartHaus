namespace CasCap.Services;

/// <inheritdoc/>
public class UbiquitiQueryService(
    ILogger<UbiquitiQueryService> logger,
    IEnumerable<IEventSink<UbiquitiEvent>> eventSinks,
    IUbiquitiQuery? ubiquitiQuery = null
    ) : IUbiquitiQueryService
{
    /// <inheritdoc/>
    public async Task<UbiquitiSnapshot> GetSnapshot()
    {
        if (ubiquitiQuery is null)
            return new UbiquitiSnapshot { SnapshotUtc = DateTime.UtcNow };

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
            DateCreatedUtc = DateTime.UtcNow,
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
