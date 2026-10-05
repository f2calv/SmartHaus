namespace CasCap.Services;

/// <summary>
/// MCP wrapper for <see cref="IDoorBirdQueryService"/> that exposes front door intercom operations as MCP tools.
/// </summary>
[McpServerToolType]
public sealed partial class FrontDoorMcpQueryService(IDoorBirdQueryService doorBirdQuerySvc)
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
    /// <remarks>
    /// Returns a <see cref="MyBlob"/> rather than the underlying <c>byte[]</c> so the agent
    /// framework's image-stripping path engages. A raw <c>byte[]</c> serialises to a base64 JSON
    /// <i>string</i>, which that path does not recognise, so a ~30 KB JPEG reached the model as
    /// ~42 K characters (~32 K tokens) and could exhaust the context window on an edge GPU.
    /// </remarks>
    [McpServerTool]
    [Description("Retrieves a historical JPEG snapshot from the front door camera's internal storage.")]
    public Task<MyBlob> GetHouseDoorHistoryImage(
        [Description("1-based index of the image (1 = most recent, max 50).")]
        int index = 1,
        [Description("Event type filter. Values: Doorbell, MotionSensor, Rfid, DoorRelay.")]
        DoorBirdEventType? doorBirdEventType = null)
        => doorBirdQuerySvc.GetHistoryImageSummary(index, doorBirdEventType);

    /// <inheritdoc cref="IDoorBirdQueryService.GetHistoryImageSummary"/>
    /// <remarks>
    /// TODO (C5 part 2): this now returns the same payload as
    /// <see cref="GetHouseDoorHistoryImage"/> — <see cref="IDoorBirdQueryService.GetHistoryImageSummary"/>
    /// is documented as omitting the raw image data but <see cref="MyBlob.bytes"/> is a required
    /// member, so the bytes are always present and the framework strips and delivers them either
    /// way. Two tools with identical behaviour but different descriptions is confusing for the
    /// model. Reconcile when the tool-result transform moves to <c>MarshalResult</c>: either give
    /// the summary a genuinely byte-free result type, or drop this tool.
    /// </remarks>
    [McpServerTool]
    [Description("Returns metadata (size, timestamp) of a historical front door snapshot without the image bytes.")]
    public Task<MyBlob> GetHouseDoorHistoryImageInfo(
        [Description("1-based index of the image (1 = most recent, max 50).")]
        int index = 1,
        [Description("Event type filter. Values: Doorbell, MotionSensor, Rfid, DoorRelay.")]
        DoorBirdEventType? doorBirdEventType = null)
        => doorBirdQuerySvc.GetHistoryImageSummary(index, doorBirdEventType);
}
