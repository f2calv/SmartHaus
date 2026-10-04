namespace CasCap.Models.Dtos;

/// <summary>A bounded DoorBird microphone capture framed as G.711 μ-law WAV audio.</summary>
public sealed record DoorBirdAudioClip
{
    /// <summary>The complete WAV file bytes.</summary>
    /// <example>UklGR...</example>
    [JsonIgnore]
    public required byte[] Bytes { get; init; }

    /// <summary>The media type of <see cref="Bytes"/>.</summary>
    /// <example>audio/wav</example>
    public string MediaType { get; init; } = "audio/wav";

    /// <summary>The captured audio duration.</summary>
    /// <example>00:00:10</example>
    public TimeSpan Duration { get; init; }

    /// <summary>The UTC time when capture completed.</summary>
    /// <example>2026-10-03T08:54:00Z</example>
    public DateTime CapturedUtc { get; init; }

    /// <summary>A generated file name suitable for an attachment.</summary>
    /// <example>doorbird-audio-2026-10-03-08-54-00-000.wav</example>
    public string FileName => $"doorbird-audio-{CapturedUtc:yyyy-MM-dd-HH-mm-ss-fff}.wav";
}
