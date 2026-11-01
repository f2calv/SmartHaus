namespace CasCap.Services;

/// <summary>
/// MCP wrapper for <see cref="IDoorBirdQueryService"/> that exposes front door intercom operations as MCP tools.
/// </summary>
[McpServerToolType]
public sealed partial class FrontDoorMcpQueryService(IDoorBirdQueryService doorBirdQuerySvc, TimeProvider timeProvider)
{
    /// <inheritdoc cref="IDoorBirdQueryService.GetSnapshot"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Current front door state — last event timestamps for doorbell, motion, RFID and relay.")]
    public Task<DoorBirdSnapshot> GetHouseDoorState() => doorBirdQuerySvc.GetSnapshot();

    /// <inheritdoc cref="IDoorBirdQueryService.GetRealTimePhoto"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Takes a live photo from the front door camera and returns the image bytes with metadata.")]
    public Task<MyBlob> GetHouseDoorPhoto() => doorBirdQuerySvc.GetRealTimePhoto();

    /// <inheritdoc cref="IDoorBirdQueryService.GetRealTimePhotoMetaDataOnly"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Takes a live photo from the front door camera and returns metadata only (size, timestamp) without image bytes.")]
    public Task<MyBlobInfo> GetHouseDoorPhotoInfo() => doorBirdQuerySvc.GetRealTimePhotoMetaDataOnly();

    /// <inheritdoc cref="IDoorBirdQueryService.UnlockFrontDoor()"/>
    /// <remarks>Waits for a person in the household chat to approve; anything else returns a refusal.</remarks>
    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [RequiresApproval]
    [Description("Unlocks the front door by triggering the electric door release. Only call this when the user explicitly asks to open the front door; never as part of describing an image or event. A person in the household chat must approve each call before the door opens.")]
    public Task<bool> UnlockHouseDoor() => doorBirdQuerySvc.UnlockFrontDoor();

    /// <inheritdoc cref="IDoorBirdQueryService.LightOn"/>
    [McpServerTool(ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Activates the infrared night-vision illuminator at the front door. Auto-deactivates after a short timeout.")]
    public Task<LightOnResponse?> EnableHouseDoorNightVision() => doorBirdQuerySvc.LightOn();

    /// <inheritdoc cref="IDoorBirdQueryService.GetVideoStreamUrl"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns the URL for the live MJPEG video stream from the front door camera.")]
    public Uri GetHouseDoorVideoStreamUrl() => doorBirdQuerySvc.GetVideoStreamUrl();

    /// <inheritdoc cref="IDoorBirdQueryService.GetHistoryImage"/>
    /// <remarks>
    /// Returns a <see cref="MyBlob"/> rather than the underlying <c>byte[]</c> so the agent
    /// framework's image-stripping path engages. A raw <c>byte[]</c> serialises to a base64 JSON
    /// <i>string</i>, which that path does not recognise, so a ~30 KB JPEG reached the model as
    /// ~42 K characters (~32 K tokens) and could exhaust the context window on an edge GPU.
    /// </remarks>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Retrieves a historical JPEG snapshot from the front door camera's internal storage.")]
    public async Task<MyBlob> GetHouseDoorHistoryImage(
        [Description("1-based index of the image (1 = most recent, max 50).")]
        int index = 1,
        [Description("Event type filter. Values: Doorbell, MotionSensor, Rfid, DoorRelay.")]
        DoorBirdEventType? doorBirdEventType = null)
        => new(
            await doorBirdQuerySvc.GetHistoryImage(index, doorBirdEventType) ?? [],
            nameof(GetHouseDoorHistoryImage),
            timeProvider.GetUtcNow().UtcDateTime);

    /// <inheritdoc cref="IDoorBirdQueryService.GetHistoryImageSummary"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns metadata (size, timestamp) of a historical front door snapshot without the image bytes.")]
    public Task<MyBlobInfo> GetHouseDoorHistoryImageInfo(
        [Description("1-based index of the image (1 = most recent, max 50).")]
        int index = 1,
        [Description("Event type filter. Values: Doorbell, MotionSensor, Rfid, DoorRelay.")]
        DoorBirdEventType? doorBirdEventType = null)
        => doorBirdQuerySvc.GetHistoryImageSummary(index, doorBirdEventType);
}
