namespace CasCap.Models;

/// <summary>Configuration for the speech-to-text boundary and the bounded voice-media policy.</summary>
/// <remarks>
/// Bound from the <c>CasCap:SpeechToTextConfig</c> section under the application configuration root.
/// Every property carries a safe default so the section may be absent entirely.
/// </remarks>
public sealed record SpeechToTextConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(SpeechToTextConfig)}";

    /// <summary>Base address of the openai-whisper-asr-webservice deployment.</summary>
    /// <remarks>
    /// Defaults to <c>http://localhost:9000</c> so no deployment coordinate is committed.
    /// Used by <see cref="CasCap.Services.WhisperAsrSpeechToTextClient"/>.
    /// </remarks>
    [Required, Url]
    public string Endpoint { get; init; } = "http://localhost:9000";

    /// <summary>ISO 639-1 language code passed to the transcription backend.</summary>
    /// <remarks>Defaults to <c>en</c>. Used by <see cref="CasCap.Services.WhisperAsrSpeechToTextClient"/>.</remarks>
    [Required, MinLength(2)]
    public string Language { get; init; } = "en";

    /// <summary>Optional model identifier reported alongside a transcription response.</summary>
    /// <remarks>Defaults to <see langword="null"/>; the backend selects its configured model.</remarks>
    public string? ModelId { get; init; }

    /// <summary>Total time budget in milliseconds for one transcription, including admission and conversion.</summary>
    /// <remarks>
    /// Defaults to <c>120000</c> ms (2 minutes), which accommodates CPU-only inference.
    /// Used by <see cref="CasCap.Services.VoiceMessageTranscriptionService"/> and
    /// <see cref="CasCap.Services.WhisperAsrSpeechToTextClient"/>.
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int TimeoutMs { get; init; } = 120_000;

    /// <summary>Maximum accepted size in bytes of the compressed attachment before any conversion.</summary>
    /// <remarks>
    /// Defaults to <c>5242880</c> (5 MiB). Enforced before any network transmission by
    /// <see cref="CasCap.Services.VoiceMessageTranscriptionService"/>.
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int MaxCompressedBytes { get; init; } = 5 * 1024 * 1024;

    /// <summary>Maximum accepted size in bytes of the decoded 16 kHz mono PCM WAV.</summary>
    /// <remarks>
    /// Defaults to <c>19200000</c> (10 minutes of 16 kHz mono signed 16-bit PCM).
    /// Used by <see cref="CasCap.Services.VoiceMessageTranscriptionService"/>.
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int MaxDecodedBytes { get; init; } = 19_200_000;

    /// <summary>Maximum accepted decoded duration in seconds.</summary>
    /// <remarks>Defaults to <c>300</c> (5 minutes). Used by <see cref="CasCap.Services.VoiceMessageTranscriptionService"/>.</remarks>
    [Range(1, int.MaxValue)]
    public int MaxDurationSeconds { get; init; } = 300;

    /// <summary>Path to the ffmpeg executable used to normalise non-WAV audio.</summary>
    /// <remarks>
    /// Defaults to <c>ffmpeg</c>, resolved through <c>PATH</c>. Used by
    /// <see cref="CasCap.Services.VoiceMessageTranscriptionService"/>.
    /// </remarks>
    [Required, MinLength(1)]
    public string FfmpegPath { get; init; } = "ffmpeg";

    /// <inheritdoc cref="VoiceProcessingMode" path="/summary"/>
    /// <remarks>
    /// Defaults to <see cref="VoiceProcessingMode.Disabled"/> so voice remains inert until the
    /// receive path is deliberately promoted.
    /// </remarks>
    public VoiceProcessingMode Mode { get; init; } = VoiceProcessingMode.Disabled;
}
