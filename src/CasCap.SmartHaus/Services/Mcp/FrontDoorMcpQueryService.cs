namespace CasCap.Services;

/// <summary>
/// MCP wrapper for <see cref="IDoorBirdQueryService"/> that exposes front door intercom operations as MCP tools.
/// </summary>
[McpServerToolType]
public partial class FrontDoorMcpQueryService(IDoorBirdQueryService doorBirdQuerySvc)
{
    /// <inheritdoc cref="IDoorBirdQueryService.GetSnapshot"/>
    [McpServerTool]
    [Description("Current front door state — last event timestamps for doorbell, motion, RFID and relay.")]
    public Task<DoorBirdSnapshot> GetHouseDoorState() => doorBirdQuerySvc.GetSnapshot();

    /// <inheritdoc cref="IDoorBirdQueryService.GetRealTimePhoto"/>
    [McpServerTool]
    [Description("Takes a live photo from the front door camera and returns the image bytes with metadata.")]
    public Task<MyBlob> GetHouseDoorPhoto() => doorBirdQuerySvc.GetRealTimePhoto();

    /// <inheritdoc cref="IDoorBirdQueryService.GetRealTimePhotoMetaDataOnly"/>
    [McpServerTool]
    [Description("Takes a live photo from the front door camera and returns metadata only (size, timestamp) without image bytes.")]
    public Task<MyBlob> GetHouseDoorPhotoInfo() => doorBirdQuerySvc.GetRealTimePhotoMetaDataOnly();

    /// <inheritdoc cref="IDoorBirdQueryService.UnlockFrontDoor()"/>
    [McpServerTool]
    [Description("Unlocks the front door by triggering the electric door release.")]
    public Task<bool> UnlockHouseDoor() => doorBirdQuerySvc.UnlockFrontDoor();

    /// <inheritdoc cref="IDoorBirdQueryService.LightOn"/>
    [McpServerTool]
    [Description("Activates the infrared night-vision illuminator at the front door. Auto-deactivates after a short timeout.")]
    public Task<LightOnResponse?> EnableHouseDoorNightVision() => doorBirdQuerySvc.LightOn();

    /// <inheritdoc cref="IDoorBirdQueryService.GetVideoStreamUrl"/>
    [McpServerTool]
    [Description("Returns the URL for the live MJPEG video stream from the front door camera.")]
    public Uri GetHouseDoorVideoStreamUrl() => doorBirdQuerySvc.GetVideoStreamUrl();

    /// <inheritdoc cref="IDoorBirdQueryService.GetHistoryImage"/>
    [McpServerTool]
    [Description("Retrieves a historical JPEG snapshot from the front door camera's internal storage.")]
    public Task<byte[]?> GetHouseDoorHistoryImage(
        [Description("1-based index of the image (1 = most recent, max 50).")]
        int index = 1,
        [Description("Event type filter. Values: Doorbell, MotionSensor, Rfid, DoorRelay.")]
        DoorBirdEventType? doorBirdEventType = null)
        => doorBirdQuerySvc.GetHistoryImage(index, doorBirdEventType);

    /// <inheritdoc cref="IDoorBirdQueryService.GetHistoryImageSummary"/>
    [McpServerTool]
    [Description("Returns metadata (size, timestamp) of a historical front door snapshot without the image bytes.")]
    public Task<MyBlob> GetHouseDoorHistoryImageInfo(
        [Description("1-based index of the image (1 = most recent, max 50).")]
        int index = 1,
        [Description("Event type filter. Values: Doorbell, MotionSensor, Rfid, DoorRelay.")]
        DoorBirdEventType? doorBirdEventType = null)
        => doorBirdQuerySvc.GetHistoryImageSummary(index, doorBirdEventType);
}
