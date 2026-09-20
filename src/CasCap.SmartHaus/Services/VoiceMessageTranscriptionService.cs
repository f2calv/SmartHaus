using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace CasCap.Services;

//ISpeechToTextClient is published as experimental (MEAI001); see WhisperAsrSpeechToTextClient.
#pragma warning disable MEAI001

/// <summary>
/// Applies the bounded voice-media policy — validation, normalisation, admission control and
/// speech-to-text — and returns a typed <see cref="VoiceTranscriptionResult"/>.
/// </summary>
/// <remarks>
/// Nothing is transmitted until the declared media type and the payload's own file signature agree
/// and every size limit passes. Accepted non-WAV audio, and WAV that is not already 16 kHz mono
/// signed 16-bit PCM, is normalised by piping it through ffmpeg standard input and standard output,
/// so audio never touches the file system. Exactly one transcription runs per instance.
/// </remarks>
public sealed partial class VoiceMessageTranscriptionService(
    ILogger<VoiceMessageTranscriptionService> logger,
    IOptions<SpeechToTextConfig> options,
    ISpeechToTextClient speechToTextSvc,
    VoiceTranscriptionMetrics metrics) : IDisposable
{
    private const int _targetSampleRate = 16_000;
    private const int _targetChannels = 1;
    private const int _targetBitsPerSample = 16;
    private const short _pcmAudioFormat = 1;
    private const int _riffHeaderLength = 12;
    private const int _chunkHeaderLength = 8;

    private static readonly FrozenSet<string> _supportedMediaTypes = new[]
    {
        WhisperAsrSpeechToTextClient.WavMediaType, "audio/x-wav", "audio/wave", "audio/vnd.wave",
        "audio/aac", "audio/mpeg", "audio/mp3", "audio/mp4", "audio/m4a", "audio/x-m4a",
        "audio/ogg", "audio/oga", "audio/opus", "audio/flac", "audio/x-flac",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] _ffmpegArguments =
    [
        "-hide_banner", "-loglevel", "error",
        "-i", "pipe:0",
        "-vn",
        "-ac", "1",
        "-ar", "16000",
        "-acodec", "pcm_s16le",
        "-f", "wav", "pipe:1"
    ];

    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Validates, normalises and transcribes one voice attachment.</summary>
    /// <param name="audio">The attachment bytes exactly as received.</param>
    /// <param name="mediaType">The media type declared by the sender.</param>
    /// <param name="cancellationToken">Cancellation token owned by the caller.</param>
    /// <returns>The typed outcome; only a successful outcome carries transcript text.</returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    public async Task<VoiceTranscriptionResult> Transcribe(byte[] audio, string mediaType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audio);

        var config = options.Value;
        if (ValidateInput(audio, mediaType, config) is { } rejection)
            return rejection;

        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromMilliseconds(config.TimeoutMs));

        var admitted = false;
        try
        {
            await _gate.WaitAsync(budget.Token);
            admitted = true;

            var transcodeStart = Stopwatch.GetTimestamp();
            var alreadyNormalised = IsNormalisedWav(audio);
            var wav = alreadyNormalised ? audio : await ToWav(audio, config.FfmpegPath, budget.Token);
            TimeSpan? transcodeDuration = alreadyNormalised ? null : Stopwatch.GetElapsedTime(transcodeStart);
            if (wav is null || wav.Length == 0)
                return Reject(VoiceTranscriptionOutcome.ConversionFailed, audio.Length);
            if (wav.Length > config.MaxDecodedBytes)
                return Reject(VoiceTranscriptionOutcome.Oversized, wav.Length);
            if (!TryReadWavFormat(wav, out var format))
                return Reject(VoiceTranscriptionOutcome.Invalid, wav.Length);
            if (format.Duration > TimeSpan.FromSeconds(config.MaxDurationSeconds))
                return Reject(VoiceTranscriptionOutcome.Oversized, wav.Length);

            var speechToTextOptions = new SpeechToTextOptions
            {
                SpeechLanguage = config.Language,
                SpeechSampleRate = _targetSampleRate,
                ModelId = config.ModelId,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    [WhisperAsrSpeechToTextClient.MediaTypePropertyKey] = WhisperAsrSpeechToTextClient.WavMediaType,
                },
            };

            using var audioStream = new MemoryStream(wav, writable: false);
            var transcriptionStart = Stopwatch.GetTimestamp();
            var response = await speechToTextSvc.GetTextAsync(audioStream, speechToTextOptions, budget.Token);
            var transcriptionDuration = Stopwatch.GetElapsedTime(transcriptionStart);

            var text = Normalise(response.Text);
            if (text.Length == 0)
                return Reject(VoiceTranscriptionOutcome.EmptyTranscript, wav.Length);

            LogTranscribed(logger, wav.Length, (int)format.Duration.TotalSeconds, text.Length,
                (int)(transcodeDuration?.TotalMilliseconds ?? 0), (int)transcriptionDuration.TotalMilliseconds);
            return Record(VoiceTranscriptionResult.Success(text, format.Duration, transcodeDuration,
                transcriptionDuration));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Reject(VoiceTranscriptionOutcome.TimedOut, audio.Length);
        }
        catch (HttpRequestException ex)
        {
            LogBackendFailed(logger, (int?)ex.StatusCode ?? 0,
                ex.StatusCode is { } statusCode && WhisperAsrSpeechToTextClient.IsTransientStatusCode(statusCode));
            return Record(VoiceTranscriptionResult.Failure(VoiceTranscriptionOutcome.BackendFailed));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            //A backend that throws something unforeseen must still be reported to the sender; letting it
            //  escape aborts the whole receive loop and the message is acknowledged but never answered.
            //Caller cancellation is deliberately excluded so shutdown still unwinds promptly.
            LogBackendFaulted(logger, ex);
            return Record(VoiceTranscriptionResult.Failure(VoiceTranscriptionOutcome.BackendFailed));
        }
        finally
        {
            if (admitted)
                _gate.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _gate.Dispose();

    #region Private helpers

    private VoiceTranscriptionResult Reject(VoiceTranscriptionOutcome outcome, int byteCount)
    {
        LogRejected(logger, outcome, byteCount);
        return Record(VoiceTranscriptionResult.Failure(outcome));
    }

    private VoiceTranscriptionResult Record(VoiceTranscriptionResult result)
    {
        metrics.Record(result);
        return result;
    }

    private VoiceTranscriptionResult? ValidateInput(byte[] audio, string mediaType, SpeechToTextConfig config)
    {
        if (string.IsNullOrWhiteSpace(mediaType) || !_supportedMediaTypes.Contains(mediaType))
            return Reject(VoiceTranscriptionOutcome.Unsupported, audio.Length);
        if (audio.Length == 0 || !SignatureMatches(audio, mediaType))
            return Reject(VoiceTranscriptionOutcome.Invalid, audio.Length);
        return audio.Length > config.MaxCompressedBytes
            ? Reject(VoiceTranscriptionOutcome.Oversized, audio.Length)
            : null;
    }

    private static bool SignatureMatches(ReadOnlySpan<byte> audio, string mediaType) => mediaType.ToLowerInvariant() switch
    {
        WhisperAsrSpeechToTextClient.WavMediaType or "audio/x-wav" or "audio/wave" or "audio/vnd.wave" => IsRiffWave(audio),
        "audio/ogg" or "audio/oga" or "audio/opus" => audio.StartsWith("OggS"u8),
        "audio/mpeg" or "audio/mp3" => IsMpegAudio(audio),
        "audio/mp4" or "audio/m4a" or "audio/x-m4a" => IsIsoBaseMedia(audio),
        //Android Signal sends AAC in both raw ADTS and MPEG-4 containers.
        "audio/aac" => IsAdts(audio) || IsIsoBaseMedia(audio),
        "audio/flac" or "audio/x-flac" => audio.StartsWith("fLaC"u8),
        _ => false
    };

    private static bool IsRiffWave(ReadOnlySpan<byte> audio) =>
        audio.Length >= _riffHeaderLength && audio.StartsWith("RIFF"u8) && audio[8..12].SequenceEqual("WAVE"u8);

    private static bool IsIsoBaseMedia(ReadOnlySpan<byte> audio) =>
        audio.Length >= _chunkHeaderLength && audio[4..8].SequenceEqual("ftyp"u8);

    private static bool IsMpegAudio(ReadOnlySpan<byte> audio) =>
        audio.StartsWith("ID3"u8) || (audio.Length >= 2 && audio[0] == 0xFF && (audio[1] & 0xE0) == 0xE0);

    private static bool IsAdts(ReadOnlySpan<byte> audio) =>
        audio.Length >= 2 && audio[0] == 0xFF && (audio[1] & 0xF6) == 0xF0;

    private static bool IsNormalisedWav(ReadOnlySpan<byte> audio) =>
        TryReadWavFormat(audio, out var format)
        && format.AudioFormat == _pcmAudioFormat
        && format.SampleRate == _targetSampleRate
        && format.Channels == _targetChannels
        && format.BitsPerSample == _targetBitsPerSample;

    //Walks the RIFF chunk list for the fmt and data chunks; duration is derived from the declared byte rate.
    private static bool TryReadWavFormat(ReadOnlySpan<byte> audio, out WavFormat format)
    {
        format = default;
        if (!IsRiffWave(audio))
            return false;

        short audioFormat = 0, channels = 0, bitsPerSample = 0;
        uint sampleRate = 0, byteRate = 0, dataLength = 0;
        var seenFormat = false;
        var offset = _riffHeaderLength;
        while (offset + _chunkHeaderLength <= audio.Length)
        {
            var chunkId = audio.Slice(offset, 4);
            var chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(audio.Slice(offset + 4, 4));
            var payload = audio[(offset + _chunkHeaderLength)..];

            if (chunkId.SequenceEqual("data"u8))
            {
                // ffmpeg cannot seek back to patch the size when writing to a pipe, so it emits a
                // placeholder length; the audio is then whatever remains after the header.
                dataLength = chunkLength > payload.Length ? (uint)payload.Length : chunkLength;
                break;
            }

            if (chunkLength > payload.Length)
                return false;

            if (ReadFormatChunk(chunkId, chunkLength, payload) is { } formatChunk)
            {
                audioFormat = formatChunk.AudioFormat;
                channels = formatChunk.Channels;
                sampleRate = formatChunk.SampleRate;
                byteRate = formatChunk.ByteRate;
                bitsPerSample = formatChunk.BitsPerSample;
                seenFormat = true;
            }

            offset += _chunkHeaderLength + (int)chunkLength + ((chunkLength & 1) == 1 ? 1 : 0);
        }

        if (!seenFormat || byteRate == 0)
            return false;

        format = new WavFormat(audioFormat, channels, (int)sampleRate, bitsPerSample,
            TimeSpan.FromSeconds((double)dataLength / byteRate));
        return true;
    }

    private static WavFormatChunk? ReadFormatChunk(ReadOnlySpan<byte> chunkId, uint chunkLength,
        ReadOnlySpan<byte> payload) =>
        chunkId.SequenceEqual("fmt "u8) && chunkLength >= 16
            ? new(
                BinaryPrimitives.ReadInt16LittleEndian(payload[..2]),
                BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(2, 2)),
                BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(4, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(8, 4)),
                BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(14, 2)))
            : null;

    private async Task<byte[]?> ToWav(byte[] audio, string ffmpegPath, CancellationToken cancellationToken)
    {
        //Length-only capture: ffmpeg diagnostics echo container metadata, so the text is never retained.
        var result = await ShellExtensions.RunProcessWithStdinAsync(ffmpegPath, _ffmpegArguments, audio,
            ProcessErrorCapture.Length, cancellationToken);

        if (result.ExitCode == -1)
        {
            LogFfmpegUnavailable(logger, nameof(ShellExtensions));
            return null;
        }

        if (!result.Success)
        {
            LogConversionFailed(logger, result.ExitCode, result.Output.Length, result.ErrorLength);
            return null;
        }

        return result.Output;
    }

    private static string Normalise(string? text) =>
        string.IsNullOrWhiteSpace(text) ? string.Empty : WhitespaceRegex().Replace(text, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private readonly record struct WavFormat(short AudioFormat, short Channels, int SampleRate,
        short BitsPerSample, TimeSpan Duration);

    private readonly record struct WavFormatChunk(short AudioFormat, short Channels, uint SampleRate,
        uint ByteRate, short BitsPerSample);

    [LoggerMessage(LogLevel.Warning, "{ClassName} rejected voice media, outcome={Outcome}, bytes={Bytes}")]
    private static partial void LogRejected(ILogger logger, VoiceTranscriptionOutcome outcome, int bytes,
        string className = nameof(VoiceMessageTranscriptionService));

    [LoggerMessage(LogLevel.Error,
        "{ClassName} transcription backend failed, StatusCode={StatusCode}, transient={Transient}")]
    private static partial void LogBackendFailed(ILogger logger, int statusCode, bool transient,
        string className = nameof(VoiceMessageTranscriptionService));

    [LoggerMessage(LogLevel.Error, "{ClassName} could not start ffmpeg, failure={Failure}")]
    private static partial void LogFfmpegUnavailable(ILogger logger, string failure,
        string className = nameof(VoiceMessageTranscriptionService));

    [LoggerMessage(LogLevel.Error, "{ClassName} speech-to-text backend faulted unexpectedly")]
    private static partial void LogBackendFaulted(ILogger logger, Exception exception,
        string className = nameof(VoiceMessageTranscriptionService));

    [LoggerMessage(LogLevel.Error,
        "{ClassName} ffmpeg conversion failed, exitCode={ExitCode}, outputBytes={OutputBytes}, errorChars={ErrorChars}")]
    private static partial void LogConversionFailed(ILogger logger, int exitCode, int outputBytes, int errorChars,
        string className = nameof(VoiceMessageTranscriptionService));

    [LoggerMessage(LogLevel.Information,
        "{ClassName} transcribed voice media, decodedBytes={DecodedBytes}, durationSeconds={DurationSeconds}, transcriptLength={TranscriptLength}, transcodeMs={TranscodeMs}, transcriptionMs={TranscriptionMs}")]
    private static partial void LogTranscribed(ILogger logger, int decodedBytes, int durationSeconds,
        int transcriptLength, int transcodeMs, int transcriptionMs,
        string className = nameof(VoiceMessageTranscriptionService));

    #endregion
}

#pragma warning restore MEAI001
