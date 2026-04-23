namespace CasCap.Abstractions;

/// <summary>
/// Defines the public query and control operations exposed by the DoorBird device service.
/// </summary>
public interface IDoorBirdQueryService
{
    /// <summary>
    /// Retrieves a snapshot of recent DoorBird device activity including last event timestamps per <see cref="DoorBirdEventType"/>.
    /// </summary>
    Task<DoorBirdSnapshot> GetSnapshot();

    /// <summary>
    /// DoorBird device will take a photo at the front door and returns the JPG wrapped in a <see cref="MyBlob"/>.
    /// </summary>
    Task<MyBlob> GetRealTimePhoto();

    /// <summary>
    /// DoorBird device will take a photo at the front door and returns metadata only (size in bytes and timestamp) without the raw image data.
    /// </summary>
    Task<MyBlob> GetRealTimePhotoMetaDataOnly();

    /// <summary>
    /// DoorBird device will return the URL for the live MJPEG video stream from the camera.
    /// </summary>
    Uri GetVideoStreamUrl();

    /// <summary>
    /// DoorBird device will trigger the front/house door catch/lock relay which will allow access.
    /// </summary>
    /// <param name="doorControllerID">Optional, e.g. abcdef, lkjhgf, etc...</param>
    /// <param name="relayName">Optional, e.g. 1, 2, 3, etc...</param>
    Task<bool> UnlockFrontDoor(string? doorControllerID = null, string? relayName = null);

    /// <summary>
    /// DoorBird device IR light will turn on.
    /// </summary>
    Task<LightOnResponse?> LightOn();

    /// <summary>
    /// Retrieves device information from the DoorBird device.
    /// </summary>
    Task<InfoResponse?> GetInfo();

    /// <summary>
    /// Restarts the DoorBird device.
    /// </summary>
    Task<RestartResponse?> Restart();

    /// <summary>
    /// Retrieves the current SIP registration status from the DoorBird device.
    /// </summary>
    Task<SipStatusResponse?> GetSipStatus();

    /// <summary>
    /// Retrieves an authenticated session from the DoorBird device.
    /// </summary>
    Task<SessionResponse?> GetSession();

    /// <summary>
    /// Retrieves the favorites list from the DoorBird device.
    /// </summary>
    Task<string?> GetFavorites();

    /// <summary>
    /// Retrieves the notification schedule from the DoorBird device.
    /// </summary>
    Task<string?> GetSchedule();

    /// <summary>
    /// DoorBird device will return a historical JPEG snapshot from internal storage.
    /// </summary>
    /// <param name="index">The 1-based index of the image (1 = most recent, max 50).</param>
    /// <param name="doorBirdEventType">Optional event type filter (doorbell or motionsensor).</param>
    Task<byte[]?> GetHistoryImage(int index = 1, DoorBirdEventType? doorBirdEventType = null);

    /// <summary>
    /// DoorBird device will return a lightweight summary (size in bytes and timestamp) of a historical JPEG snapshot from internal storage, without the raw image data.
    /// </summary>
    /// <param name="index">The 1-based index of the image (1 = most recent, max 50).</param>
    /// <param name="doorBirdEventType">Optional event type filter (doorbell or motionsensor).</param>
    Task<MyBlob> GetHistoryImageSummary(int index = 1, DoorBirdEventType? doorBirdEventType = null);

    /// <summary>
    /// Retrieves the list of active notification subscriptions from the DoorBird device.
    /// </summary>
    Task<NotificationListResponse?> ListNotifications();

    /// <summary>
    /// Subscribes a URL to receive DoorBird event notifications.
    /// </summary>
    /// <param name="subscriberUrl">The URL to receive notifications.</param>
    /// <param name="eventType">The event type to subscribe to.</param>
    /// <param name="relaxation">Optional relaxation time in seconds between repeat notifications.</param>
    Task<bool> SubscribeNotification(string subscriberUrl, string eventType, int? relaxation = null);

    /// <summary>
    /// Removes a notification subscription from the DoorBird device.
    /// </summary>
    /// <param name="subscriberUrl">The URL to unsubscribe.</param>
    /// <param name="eventType">The event type to unsubscribe from.</param>
    Task<bool> UnsubscribeNotification(string subscriberUrl, string eventType);

    /// <summary>
    /// Sends a DoorBird alert event (doorbell, motion sensor, RFID or door relay) to all configured event sinks.
    /// </summary>
    /// <param name="type"><inheritdoc cref="DoorBirdEventType" path="/summary"/></param>
    Task SendAlert(DoorBirdEventType type);
}
