namespace CasCap.Services;

/// <summary>
/// Publishes per-message voice pipeline measurements — audio duration, ffmpeg normalisation and
/// speech-to-text — as OpenTelemetry instruments.
/// </summary>
/// <remarks>
/// The dimensions are the bounded <see cref="VoiceTranscriptionOutcome"/> and the configured
/// <see cref="SpeechToTextProvider"/>, which is what makes the backends comparable. No sender, group,
/// attachment identifier, media type, filename or transcript is ever attached to a measurement.
/// The speed instruments report a realtime factor: seconds of audio processed per second of wall
/// clock, so a value above one means the stage ran faster than playback.
/// </remarks>
public sealed class VoiceTranscriptionMetrics
{
    /// <summary>The outcome dimension carried by every instrument.</summary>
    public const string OutcomeTagName = "outcome";

    /// <summary>The speech-to-text backend dimension carried by every instrument.</summary>
    public const string ProviderTagName = "provider";

    private readonly string _provider;

    private readonly Counter<long> _transcriptions;
    private readonly Histogram<double> _audioDuration;
    private readonly Histogram<double> _transcodeDuration;
    private readonly Histogram<double> _transcodeSpeed;
    private readonly Histogram<double> _transcriptionDuration;
    private readonly Histogram<double> _transcriptionSpeed;

    /// <summary>Initializes a new instance of the <see cref="VoiceTranscriptionMetrics"/> class.</summary>
    public VoiceTranscriptionMetrics(IOptions<EdgeHardwareConfig> edgeHardwareConfig,
        IOptions<SpeechToTextConfig> speechToTextConfig, IMeterFactory meterFactory)
    {
        //The provider cannot change without a restart, so it is resolved once rather than per measurement.
        _provider = speechToTextConfig.Value.Provider.ToString();
        var prefix = edgeHardwareConfig.Value.MetricNamePrefix;
        var meter = meterFactory.Create(prefix);

        _transcriptions = meter.CreateCounter<long>($"{prefix}.voice.transcriptions",
            unit: "{message}", description: "Voice messages that completed the transcription pipeline");

        _audioDuration = meter.CreateHistogram<double>($"{prefix}.voice.audio.duration",
            unit: "s", description: "Playback duration of the decoded voice audio");

        _transcodeDuration = meter.CreateHistogram<double>($"{prefix}.voice.transcode.duration",
            unit: "s", description: "Wall-clock time spent normalising voice audio to WAV with ffmpeg");

        _transcodeSpeed = meter.CreateHistogram<double>($"{prefix}.voice.transcode.speed",
            unit: "1", description: "ffmpeg normalisation speed as a realtime factor");

        _transcriptionDuration = meter.CreateHistogram<double>($"{prefix}.voice.transcription.duration",
            unit: "s", description: "Wall-clock time spent in the speech-to-text backend");

        _transcriptionSpeed = meter.CreateHistogram<double>($"{prefix}.voice.transcription.speed",
            unit: "1", description: "Speech-to-text speed as a realtime factor");
    }

    /// <summary>Records one completed pipeline attempt, whatever its outcome.</summary>
    /// <param name="result">The transcription result; absent measurements are skipped.</param>
    /// <remarks>A rejected message still increments the counter, so the outcome mix stays visible.</remarks>
    public void Record(VoiceTranscriptionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var tags = new TagList
        {
            { OutcomeTagName, result.Outcome.ToString() },
            { ProviderTagName, _provider },
        };
        _transcriptions.Add(1, tags);

        if (result.AudioDuration is { } audio)
            _audioDuration.Record(audio.TotalSeconds, tags);

        //A null transcode duration means the sender's audio was already a conforming WAV.
        if (result.TranscodeDuration is { } transcode)
        {
            _transcodeDuration.Record(transcode.TotalSeconds, tags);
            RecordSpeed(_transcodeSpeed, result.AudioDuration, transcode, tags);
        }

        if (result.TranscriptionDuration is { } transcription)
        {
            _transcriptionDuration.Record(transcription.TotalSeconds, tags);
            RecordSpeed(_transcriptionSpeed, result.AudioDuration, transcription, tags);
        }
    }

    private static void RecordSpeed(Histogram<double> histogram, TimeSpan? audioDuration, TimeSpan elapsed,
        in TagList tags)
    {
        if (audioDuration is { } audio && elapsed > TimeSpan.Zero)
            histogram.Record(audio.TotalSeconds / elapsed.TotalSeconds, tags);
    }
}
