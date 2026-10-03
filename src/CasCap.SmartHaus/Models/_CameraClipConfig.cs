namespace CasCap.Models;

/// <summary>Configuration for bounded event clips retrieved from MediaMTX playback.</summary>
public sealed record CameraClipConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(CameraClipConfig)}";

    /// <summary>Whether camera event clip capture is enabled.</summary>
    public bool Enabled { get; init; }

    /// <summary>Private MediaMTX playback base address.</summary>
    [Required, Url]
    public string PlaybackBaseAddress { get; init; } = "http://localhost:9996";

    /// <summary>Private controller camera identifiers mapped to logical clip sources.</summary>
    [ValidateObjectMembers]
    public Dictionary<string, CameraClipSourceConfig> Sources { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Optional DoorBird clip source.</summary>
    [ValidateObjectMembers]
    public CameraClipSourceConfig? DoorBirdSource { get; init; }

    /// <summary>Seconds included before the event timestamp.</summary>
    [Range(0, 60)]
    public int PreRollSeconds { get; init; } = 5;

    /// <summary>Seconds included after the event timestamp.</summary>
    [Range(1, 60)]
    public int PostRollSeconds { get; init; } = 10;

    /// <summary>Maximum downloaded and remuxed clip size.</summary>
    [Range(1, 100 * 1024 * 1024)]
    public int MaximumClipBytes { get; init; } = 12 * 1024 * 1024;

    /// <summary>Maximum queued camera events.</summary>
    [Range(1, 1_000)]
    public int QueueCapacity { get; init; } = 32;

    /// <summary>Playback download and remux time budget in milliseconds.</summary>
    [Range(1_000, 300_000)]
    public int ProcessingTimeoutMs { get; init; } = 30_000;

    /// <summary>Writable directory used for bounded source and remux files.</summary>
    [Required, MinLength(1)]
    public string WorkingDirectory { get; init; } = "/tmp/camera-clips";

    /// <summary>FFmpeg executable path.</summary>
    [Required, MinLength(1)]
    public string FfmpegPath { get; init; } = "ffmpeg";
}

/// <summary>Logical clip source associated with a private camera identifier.</summary>
public sealed record CameraClipSourceConfig
{
    /// <summary>Operator-facing camera name used in notifications.</summary>
    /// <example>NorthEast</example>
    [Required, MinLength(1)]
    public required string DisplayName { get; init; }

    /// <summary>MediaMTX playback path.</summary>
    /// <example>camera-medium</example>
    [Required, MinLength(1)]
    public required string Path { get; init; }

    /// <summary>Minimum interval between accepted events for this camera.</summary>
    /// <example>30</example>
    [Range(0, 86_400)]
    public int CooldownSeconds { get; init; } = 30;
}
