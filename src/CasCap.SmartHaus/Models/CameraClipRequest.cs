namespace CasCap.Models;

internal sealed record CameraClipRequest(
    Guid EventId,
    DateTime TimestampUtc,
    string EventType,
    CameraClipSourceConfig Source,
    string MediaSource,
    byte[]? Thumbnail,
    string? ThumbnailMimeType,
    string Metadata)
{
    internal static CameraClipRequest FromUbiquiti(
        UbiquitiEvent @event,
        CameraClipSourceConfig source)
        => new(
            @event.EventId,
            @event.DateCreatedUtc,
            @event.UbiquitiEventType.ToString(),
            source,
            "Ubiquiti",
            @event.Thumbnail,
            @event.ThumbnailMimeType,
            (@event with { Thumbnail = null, SourceCameraId = null }).ToJson());

    internal static CameraClipRequest FromDoorBird(
        DoorBirdEvent @event,
        CameraClipSourceConfig source)
        => new(
            @event.EventId,
            @event.DateCreatedUtc,
            @event.DoorBirdEventType.ToString(),
            source,
            "DoorBird",
            @event.bytes,
            "image/jpeg",
            (@event with { bytes = null }).ToJson());
}
