namespace CasCap.Models;

/// <summary>
/// Represents a DoorBird event with an associated image snapshot.
/// </summary>
public record DoorBirdEvent
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// The type of DoorBird event that occurred.
    /// </summary>
    public DoorBirdEventType DoorBirdEventType { get; init; }

    /// <summary>
    /// The UTC timestamp when the event was created.
    /// </summary>
    public DateTime DateCreatedUtc { get; init; }

    /// <summary>
    /// The JPEG image bytes captured at the time of the event, or <see langword="null"/> if no image was captured.
    /// </summary>
    public byte[]? bytes { get; init; }

    /// <summary>
    /// The generated file name for the captured image, or <see langword="null"/> if no image was captured.
    /// </summary>
    public string? FileName { get { return bytes is null ? null : $"doorbird_{DateCreatedUtc:yyyy-MM-dd-HH-mm-ss-fff}_{DoorBirdEventType}.jpg"; } }

    /// <inheritdoc/>
    public override string ToString()
        => $"{DoorBirdEventType}, @ {DateCreatedUtc:yyyy-MM-dd HH:mm:ss}{(bytes is null ? string.Empty : bytes.Length)}";
}
