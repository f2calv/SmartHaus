namespace CasCap.Models;

/// <summary>
/// Source-agnostic media event written to a Redis Stream for consumption by
/// <see cref="CasCap.Services.MediaAnalysisBgService"/>. Each event references
/// binary content cached in Redis and carries enough metadata for the service to
/// route analysis to the appropriate domain agent.
/// </summary>
public record MediaEvent
{
    /// <summary>Identifies the subsystem that produced the media (e.g. <c>"DoorBird"</c>, <c>"Ubiquiti"</c>).</summary>
    [Required, MinLength(1)]
    public required string Source { get; init; }

    /// <summary>Event classification from the source (e.g. <c>"Motion"</c>, <c>"Doorbell"</c>, <c>"PersonDetected"</c>).</summary>
    [Required, MinLength(1)]
    public required string EventType { get; init; }

    /// <summary>Reference to the cached media bytes in Redis.</summary>
    [Required, ValidateObjectMembers]
    public required MediaReference Media { get; init; }

    /// <summary>Discriminator that tells the analysis service how to handle the payload.</summary>
    public required MediaType MediaType { get; init; }

    /// <summary>UTC timestamp when the event was produced by the source.</summary>
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Optional source-specific JSON metadata (e.g. serialized <c>DoorBirdEvent</c>).</summary>
    public string? Metadata { get; init; }
}
