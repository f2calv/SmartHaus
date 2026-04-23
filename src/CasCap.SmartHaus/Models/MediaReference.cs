namespace CasCap.Models;

/// <summary>
/// Pointer to binary media cached in Redis. Used by both the media analysis pipeline
/// (<see cref="MediaEvent"/>) and the comms pipeline (<see cref="CommsEvent.JsonPayload"/>)
/// to reference cached bytes without carrying them inline.
/// </summary>
public record MediaReference
{
    /// <summary>Redis key where the media bytes are cached (with a TTL).</summary>
    [Required, MinLength(1)]
    public required string MediaRedisKey { get; init; }

    /// <summary>MIME type of the media (e.g. <c>"image/jpeg"</c>).</summary>
    public string? MimeType { get; init; }

    /// <summary>Suggested file name for the attachment.</summary>
    public string? FileName { get; init; }
}
