using System.Text.Json.Serialization;

namespace CasCap.Models;

/// <summary>
/// Represents an event from a Ubiquiti UniFi Protect camera such as motion detection or smart detection.
/// </summary>
public sealed record UbiquitiEvent
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// The type of Ubiquiti camera event that occurred.
    /// </summary>
    public UbiquitiEventType UbiquitiEventType { get; init; }

    /// <summary>
    /// The UTC timestamp when the event was created.
    /// </summary>
    public DateTime DateCreatedUtc { get; init; }

    /// <summary>
    /// The identifier of the camera that produced the event, or <see langword="null"/> if unknown.
    /// </summary>
    public string? CameraId { get; init; }

    /// <summary>
    /// The display name of the camera that produced the event, or <see langword="null"/> if unknown.
    /// </summary>
    public string? CameraName { get; init; }

    /// <summary>
    /// The confidence score of the detection (0.0–1.0), or <see langword="null"/> for non-smart events.
    /// </summary>
    public double? Score { get; init; }

    /// <summary>
    /// Thumbnail bytes supplied by UniFi Protect, or <see langword="null"/> when thumbnails are disabled.
    /// The bytes are excluded from serialized event metadata.
    /// </summary>
    [JsonIgnore]
    public byte[]? Thumbnail { get; init; }

    /// <summary>The MIME type of <see cref="Thumbnail"/>, when available.</summary>
    public string? ThumbnailMimeType { get; init; }

    /// <inheritdoc/>
    public override string ToString()
        => $"{UbiquitiEventType}, camera={CameraName ?? CameraId ?? "unknown"}, @ {DateCreatedUtc:yyyy-MM-dd HH:mm:ss}, thumbnailBytes={Thumbnail?.Length ?? 0}";
}
