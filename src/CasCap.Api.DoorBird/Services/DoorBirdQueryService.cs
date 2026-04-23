namespace CasCap.Services;

/// <inheritdoc/>
public class DoorBirdQueryService(
    ILogger<DoorBirdQueryService> logger,
    IOptions<DoorBirdConfig> doorBirdConfig,
    DoorBirdClientService doorBirdClientSvc,
    IEnumerable<IEventSink<DoorBirdEvent>> eventSinks,
    IDoorBirdQuery? doorBirdQuery = null
    ) : IDoorBirdQueryService
{
    /// <inheritdoc/>
    public async Task<DoorBirdSnapshot> GetSnapshot()
    {
        if (doorBirdQuery is null)
            return new DoorBirdSnapshot { SnapshotUtc = DateTime.UtcNow };

        return await doorBirdQuery.GetSnapshot();
    }

    /// <inheritdoc/>
    public async Task<MyBlob> GetRealTimePhoto()
    {
        var bytes = await doorBirdClientSvc.GetImage();
        return new MyBlob(bytes ?? [], nameof(GetRealTimePhoto));
    }

    /// <inheritdoc/>
    public async Task<MyBlob> GetRealTimePhotoMetaDataOnly()
    {
        var bytes = await doorBirdClientSvc.GetImage();
        return new MyBlob(bytes ?? [], nameof(GetRealTimePhotoMetaDataOnly));
    }

    /// <inheritdoc/>
    public Uri GetVideoStreamUrl() => doorBirdClientSvc.GetVideoStreamUrl();

    /// <inheritdoc/>
    public async Task<bool> UnlockFrontDoor(string? doorControllerID = null, string? relayName = null)
    {
        doorControllerID ??= doorBirdConfig.Value.DoorControllerID;
        relayName ??= doorBirdConfig.Value.DoorControllerRelayID;
        var result = await doorBirdClientSvc.TriggerRelay(doorControllerID, relayName);
        if (result)
            await SendAlert(DoorBirdEventType.DoorRelay);
        return result;
    }

    /// <inheritdoc/>
    public Task<LightOnResponse?> LightOn() => doorBirdClientSvc.LightOn();

    /// <inheritdoc/>
    public async Task<InfoResponse?> GetInfo() => await doorBirdClientSvc.GetInfo();

    /// <inheritdoc/>
    public async Task<RestartResponse?> Restart() => await doorBirdClientSvc.Restart();

    /// <inheritdoc/>
    public async Task<SipStatusResponse?> GetSipStatus() => await doorBirdClientSvc.GetSipStatus();

    /// <inheritdoc/>
    public async Task<SessionResponse?> GetSession() => await doorBirdClientSvc.GetSession();

    /// <inheritdoc/>
    public async Task<string?> GetFavorites() => await doorBirdClientSvc.GetFavorites();

    /// <inheritdoc/>
    public async Task<string?> GetSchedule() => await doorBirdClientSvc.GetSchedule();

    /// <inheritdoc/>
    public Task<byte[]?> GetHistoryImage(
        int index = 1,
        DoorBirdEventType? doorBirdEventType = null)
        => doorBirdClientSvc.GetHistoryImage(index, doorBirdEventType);

    /// <inheritdoc/>
    public async Task<MyBlob> GetHistoryImageSummary(
        int index = 1,
        DoorBirdEventType? doorBirdEventType = null)
    {
        var bytes = await doorBirdClientSvc.GetHistoryImage(index, doorBirdEventType);
        return new MyBlob(bytes ?? [], nameof(GetHistoryImageSummary));
    }

    /// <inheritdoc/>
    public async Task<NotificationListResponse?> ListNotifications() => await doorBirdClientSvc.ListNotifications();

    /// <inheritdoc/>
    public async Task<bool> SubscribeNotification(string subscriberUrl, string eventType, int? relaxation = null)
        => await doorBirdClientSvc.SubscribeNotification(subscriberUrl, eventType, relaxation);

    /// <inheritdoc/>
    public async Task<bool> UnsubscribeNotification(string subscriberUrl, string eventType)
        => await doorBirdClientSvc.UnsubscribeNotification(subscriberUrl, eventType);

    /// <summary>
    /// Sends a DoorBird alert event (doorbell, motion sensor, RFID or door relay) to all configured event sinks.
    /// </summary>
    /// <param name="type"><inheritdoc cref="DoorBirdEventType" path="/summary"/></param>
    public async Task SendAlert(DoorBirdEventType type)
    {
        logger.LogInformation("{ClassName} sending alert for event type {EventType}", nameof(DoorBirdQueryService), type);

        byte[]? bytes = null;
        if (type is not DoorBirdEventType.DoorRelay)
            bytes = await doorBirdClientSvc.GetImage();

        var doorBirdEvent = new DoorBirdEvent
        {
            DoorBirdEventType = type,
            DateCreatedUtc = DateTime.UtcNow,
            bytes = bytes
        };

        var sinkTasks = new List<Task>(eventSinks.Count());
        foreach (var eventSink in eventSinks)
            sinkTasks.Add(eventSink.WriteEvent(doorBirdEvent));
        await Task.WhenAny(sinkTasks.ToArray());//Note: WhenAny instead of WhenAll because AI takes time?
    }
}
