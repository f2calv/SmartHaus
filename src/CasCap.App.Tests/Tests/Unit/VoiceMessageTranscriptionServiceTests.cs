using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;
using System.Net;
using System.Text;

namespace CasCap.Tests.Unit;

//ISpeechToTextClient is published as experimental (MEAI001); see WhisperAsrSpeechToTextClient.
#pragma warning disable MEAI001

/// <summary>
/// Policy and failure-path tests for <see cref="VoiceMessageTranscriptionService"/>. Every fixture is
/// synthesised in-process; no recording is committed and no ffmpeg binary is required.
/// </summary>
[Trait("Category", "SpeechToText")]
public class VoiceMessageTranscriptionServiceTests
{
    private const string _wav = "audio/wav";
    private const string _ogg = "audio/ogg";
    private const string _missingFfmpeg = "ffmpeg-not-installed-for-tests";

    [Theory]
    [InlineData("audio/basic")]
    [InlineData("image/png")]
    [InlineData("application/octet-stream")]
    [InlineData("")]
    public async Task Transcribe_UnsupportedMediaType(string mediaType)
    {
        var stt = new StubSpeechToTextClient();
        using var svc = CreateService(stt);

        var result = await svc.Transcribe(CreateWav(), mediaType, TestContext.Current.CancellationToken);

        Assert.Equal(VoiceTranscriptionOutcome.Unsupported, result.Outcome);
        Assert.Null(result.Text);
        Assert.Equal(0, stt.CallCount);
    }

    [Fact]
    public async Task Transcribe_EmptyPayload()
    {
        var stt = new StubSpeechToTextClient();
        using var svc = CreateService(stt);

        var result = await svc.Transcribe([], _wav, TestContext.Current.CancellationToken);

        Assert.Equal(VoiceTranscriptionOutcome.Invalid, result.Outcome);
        Assert.Equal(0, stt.CallCount);
    }

    [Theory]
    [InlineData(_wav)]
    [InlineData("audio/aac")]
    [InlineData("audio/flac")]
    [InlineData("audio/mp4")]
    public async Task Transcribe_SignatureContradictsDeclaredMediaType(string mediaType)
    {
        var stt = new StubSpeechToTextClient();
        using var svc = CreateService(stt);

        var result = await svc.Transcribe(CreateOgg(), mediaType, TestContext.Current.CancellationToken);

        Assert.Equal(VoiceTranscriptionOutcome.Invalid, result.Outcome);
        Assert.Equal(0, stt.CallCount);
    }

    [Fact]
    public async Task Transcribe_CompressedBytesOverLimit()
    {
        var stt = new StubSpeechToTextClient();
        using var svc = CreateService(stt, new SpeechToTextConfig { MaxCompressedBytes = 64 });

        var result = await svc.Transcribe(CreateWav(seconds: 1), _wav, TestContext.Current.CancellationToken);

        Assert.Equal(VoiceTranscriptionOutcome.Oversized, result.Outcome);
        Assert.Equal(0, stt.CallCount);
    }

    [Fact]
    public async Task Transcribe_DecodedBytesOverLimit()
    {
        var stt = new StubSpeechToTextClient();
        using var svc = CreateService(stt, new SpeechToTextConfig { MaxDecodedBytes = 64 });

        var result = await svc.Transcribe(CreateWav(seconds: 1), _wav, TestContext.Current.CancellationToken);

        Assert.Equal(VoiceTranscriptionOutcome.Oversized, result.Outcome);
        Assert.Equal(0, stt.CallCount);
    }

    [Fact]
    public async Task Transcribe_DurationOverLimit()
    {
        var stt = new StubSpeechToTextClient();
        using var svc = CreateService(stt, new SpeechToTextConfig { MaxDurationSeconds = 2 });

        var result = await svc.Transcribe(CreateWav(seconds: 5), _wav, TestContext.Current.CancellationToken);

        Assert.Equal(VoiceTranscriptionOutcome.Oversized, result.Outcome);
        Assert.Equal(0, stt.CallCount);
    }

    [Theory]
    [InlineData(_ogg, 16_000)]
    [InlineData(_wav, 44_100)]
    public async Task Transcribe_ConversionFailed(string mediaType, int sampleRate)
    {
        var stt = new StubSpeechToTextClient();
        using var svc = CreateService(stt, new SpeechToTextConfig { FfmpegPath = _missingFfmpeg });
        var audio = mediaType is _ogg ? CreateOgg() : CreateWav(sampleRate: sampleRate);

        var result = await svc.Transcribe(audio, mediaType, TestContext.Current.CancellationToken);

        Assert.Equal(VoiceTranscriptionOutcome.ConversionFailed, result.Outcome);
        Assert.Equal(0, stt.CallCount);
    }

    [Theory]
    [InlineData("hello there", "hello there")]
    [InlineData("  hello   there  ", "hello there")]
    [InlineData("hello\r\n\tthere", "hello there")]
    public async Task Transcribe_NormalisesTranscript(string backendText, string expected)
    {
        var stt = new StubSpeechToTextClient { Responder = (_, _, _) => Task.FromResult(new SpeechToTextResponse(backendText)) };
        using var svc = CreateService(stt);

        var result = await svc.Transcribe(CreateWav(), _wav, TestContext.Current.CancellationToken);

        Assert.Equal(VoiceTranscriptionOutcome.Success, result.Outcome);
        Assert.True(result.TranscriptAvailable);
        Assert.Equal(expected, result.Text);
        Assert.Equal(1, stt.CallCount);
    }

    [Fact]
    public async Task Transcribe_AcceptsPipedWavWithPlaceholderLengths()
    {
        var stt = new StubSpeechToTextClient();
        using var svc = CreateService(stt);

        var result = await svc.Transcribe(CreatePipedWav(seconds: 2), _wav, TestContext.Current.CancellationToken);

        Assert.Equal(VoiceTranscriptionOutcome.Success, result.Outcome);
        Assert.Equal(2, result.AudioDuration!.Value.TotalSeconds, tolerance: 0.01);
        Assert.Equal(1, stt.CallCount);
    }

    [Fact]
    public async Task Transcribe_RecordsStageTimings()    {
        var stt = new StubSpeechToTextClient();
        using var svc = CreateService(stt);

        var result = await svc.Transcribe(CreateWav(seconds: 3), _wav, TestContext.Current.CancellationToken);

        Assert.Equal(VoiceTranscriptionOutcome.Success, result.Outcome);
        Assert.Equal(3, result.AudioDuration!.Value.TotalSeconds, precision: 1);
        //A conforming WAV skips ffmpeg entirely, which is what a null transcode duration means.
        Assert.Null(result.TranscodeDuration);
        Assert.NotNull(result.TranscriptionDuration);
    }

    [Fact]
    public async Task Transcribe_SendsNormalisedWavMetadataToBackend()
    {
        var stt = new StubSpeechToTextClient { Responder = (_, _, _) => Task.FromResult(new SpeechToTextResponse("ok")) };
        using var svc = CreateService(stt, new SpeechToTextConfig { Language = "de" });

        await svc.Transcribe(CreateWav(), _wav, TestContext.Current.CancellationToken);

        Assert.Equal("de", stt.LastOptions!.SpeechLanguage);
        Assert.True(stt.LastOptions.AdditionalProperties!
            .TryGetValue(WhisperAsrSpeechToTextClient.MediaTypePropertyKey, out var mediaType));
        Assert.Equal(WhisperAsrSpeechToTextClient.WavMediaType, mediaType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    public async Task Transcribe_EmptyTranscript(string backendText)
    {
        var stt = new StubSpeechToTextClient { Responder = (_, _, _) => Task.FromResult(new SpeechToTextResponse(backendText)) };
        using var svc = CreateService(stt);

        var result = await svc.Transcribe(CreateWav(), _wav, TestContext.Current.CancellationToken);

        Assert.Equal(VoiceTranscriptionOutcome.EmptyTranscript, result.Outcome);
        Assert.Null(result.Text);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Transcribe_BackendFailure(HttpStatusCode statusCode)
    {
        var stt = new StubSpeechToTextClient
        {
            Responder = (_, _, _) => throw new HttpRequestException("backend failed", null, statusCode),
        };
        using var svc = CreateService(stt);

        var result = await svc.Transcribe(CreateWav(), _wav, TestContext.Current.CancellationToken);

        Assert.Equal(VoiceTranscriptionOutcome.BackendFailed, result.Outcome);
        Assert.Null(result.Text);
    }

    [Fact]
    public async Task Transcribe_TimeoutReleasesAdmission()
    {
        var stt = new StubSpeechToTextClient
        {
            Responder = async (_, _, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new SpeechToTextResponse("unreachable");
            },
        };
        using var svc = CreateService(stt, new SpeechToTextConfig { TimeoutMs = 200 });

        var timedOut = await svc.Transcribe(CreateWav(), _wav, TestContext.Current.CancellationToken);
        Assert.Equal(VoiceTranscriptionOutcome.TimedOut, timedOut.Outcome);

        //A second attempt proves the semaphore was released by the timed-out one.
        stt.Responder = (_, _, _) => Task.FromResult(new SpeechToTextResponse("recovered"));
        var second = await svc.Transcribe(CreateWav(), _wav, TestContext.Current.CancellationToken);

        Assert.Equal(VoiceTranscriptionOutcome.Success, second.Outcome);
        Assert.Equal("recovered", second.Text);
    }

    [Fact]
    public async Task Transcribe_CallerCancellationPropagatesAndReleasesAdmission()
    {
        var stt = new StubSpeechToTextClient
        {
            Responder = async (_, _, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new SpeechToTextResponse("unreachable");
            },
        };
        using var svc = CreateService(stt);
        using var cts = new CancellationTokenSource();

        var pending = svc.Transcribe(CreateWav(), _wav, cts.Token);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        stt.Responder = (_, _, _) => Task.FromResult(new SpeechToTextResponse("recovered"));
        var second = await svc.Transcribe(CreateWav(), _wav, TestContext.Current.CancellationToken);

        Assert.Equal(VoiceTranscriptionOutcome.Success, second.Outcome);
    }

    [Fact]
    public async Task Transcribe_SerialisesConcurrentRequests()
    {
        var stt = new StubSpeechToTextClient
        {
            Responder = async (_, _, ct) =>
            {
                await Task.Delay(50, ct);
                return new SpeechToTextResponse("ok");
            },
        };
        using var svc = CreateService(stt);
        var audio = CreateWav();
        var token = TestContext.Current.CancellationToken;

        var results = await Task.WhenAll(
            svc.Transcribe(audio, _wav, token),
            svc.Transcribe(audio, _wav, token),
            svc.Transcribe(audio, _wav, token));

        Assert.All(results, r => Assert.Equal(VoiceTranscriptionOutcome.Success, r.Outcome));
        Assert.Equal(3, stt.CallCount);
        Assert.Equal(1, stt.MaxConcurrency);
    }

    [Fact]
    public async Task Transcribe_ConvertsRealAacToNormalisedWav()
    {
        var aac = await CreateAac(seconds: 2, TestContext.Current.CancellationToken);
        Assert.SkipWhen(aac.Length == 0, "ffmpeg is not installed here; this runs inside the runtime image.");

        byte[]? delivered = null;
        var stt = new StubSpeechToTextClient
        {
            Responder = async (stream, _, ct) =>
            {
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer, ct);
                delivered = buffer.ToArray();
                return new SpeechToTextResponse("ok");
            }
        };
        using var svc = CreateService(stt);

        var result = await svc.Transcribe(aac, "audio/aac", TestContext.Current.CancellationToken);

        Assert.Equal(VoiceTranscriptionOutcome.Success, result.Outcome);
        Assert.NotNull(result.TranscodeDuration);
        Assert.Equal(2, result.AudioDuration!.Value.TotalSeconds, tolerance: 0.5);

        var (audioFormat, channels, sampleRate, bitsPerSample) = ReadWavFormat(delivered!);
        Assert.Equal(1, audioFormat);
        Assert.Equal(1, channels);
        Assert.Equal(16_000, sampleRate);
        Assert.Equal(16, bitsPerSample);
    }

    #region Private helpers

    /// <summary>Generates silent ADTS AAC with ffmpeg, or an empty array when ffmpeg is unavailable.</summary>
    private static async Task<byte[]> CreateAac(int seconds, CancellationToken cancellationToken)
    {
        string[] arguments =
        [
            "-hide_banner", "-loglevel", "error",
            "-f", "lavfi", "-i", "anullsrc=r=44100:cl=stereo",
            "-t", seconds.ToString(CultureInfo.InvariantCulture),
            "-c:a", "aac", "-f", "adts", "pipe:1"
        ];
        var result = await ShellExtensions.RunProcessWithStdinAsync("ffmpeg", arguments, [],
            ProcessErrorCapture.Text, cancellationToken);
        return result.Success ? result.Output : [];
    }

    private static (short AudioFormat, short Channels, int SampleRate, short BitsPerSample) ReadWavFormat(byte[] wav)
    {
        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));

        //ffmpeg may emit LIST or other chunks, so seek the fmt chunk rather than assuming it is first.
        var offset = 12;
        while (Encoding.ASCII.GetString(wav, offset, 4) != "fmt ")
            offset += 8 + BitConverter.ToInt32(wav, offset + 4);

        var body = offset + 8;
        return (BitConverter.ToInt16(wav, body), BitConverter.ToInt16(wav, body + 2),
            BitConverter.ToInt32(wav, body + 4), BitConverter.ToInt16(wav, body + 14));
    }

    private static VoiceMessageTranscriptionService CreateService(ISpeechToTextClient stt, SpeechToTextConfig? config = null) =>
        new(NullLogger<VoiceMessageTranscriptionService>.Instance, Options.Create(config ?? new SpeechToTextConfig()), stt,
            TestMetrics.Voice());

    /// <summary>Builds a synthetic silent RIFF/WAVE payload with the requested format.</summary>
    private static byte[] CreateWav(int sampleRate = 16_000, short channels = 1, short bitsPerSample = 16, double seconds = 1)
    {
        var blockAlign = (short)(channels * bitsPerSample / 8);
        var byteRate = sampleRate * blockAlign;
        var dataLength = (int)(byteRate * seconds);

        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer, Encoding.ASCII, leaveOpen: true);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataLength);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("data"u8);
        writer.Write(dataLength);
        writer.Write(new byte[dataLength]);
        writer.Flush();
        return buffer.ToArray();
    }

    /// <summary>
    /// Builds what ffmpeg emits when its WAV output is a pipe: it cannot seek back to patch the
    /// RIFF and data lengths, so both are left as the placeholder <c>0xFFFFFFFF</c>, and a LIST
    /// chunk sits between <c>fmt </c> and <c>data</c>.
    /// </summary>
    private static byte[] CreatePipedWav(int sampleRate = 16_000, short channels = 1, short bitsPerSample = 16,
        double seconds = 1)
    {
        var blockAlign = (short)(channels * bitsPerSample / 8);
        var byteRate = sampleRate * blockAlign;
        var dataLength = (int)(byteRate * seconds);
        ReadOnlySpan<byte> listPayload = "INFOISFT\u0010\0\0\0Lavf60.16.100\0\0\0"u8;

        using var buffer = new MemoryStream();
        using var writer = new BinaryWriter(buffer, Encoding.ASCII, leaveOpen: true);
        writer.Write("RIFF"u8);
        writer.Write(uint.MaxValue);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write("LIST"u8);
        writer.Write(listPayload.Length);
        writer.Write(listPayload);
        writer.Write("data"u8);
        writer.Write(uint.MaxValue);
        writer.Write(new byte[dataLength]);
        writer.Flush();
        return buffer.ToArray();
    }

    private static byte[] CreateOgg()
    {
        var payload = new byte[256];
        "OggS"u8.CopyTo(payload);
        return payload;
    }

    /// <summary>Records invocation counts and peak concurrency so admission control can be asserted.</summary>
    private sealed class StubSpeechToTextClient : ISpeechToTextClient
    {
        private int _inFlight;

        public Func<Stream, SpeechToTextOptions?, CancellationToken, Task<SpeechToTextResponse>> Responder { get; set; } =
            (_, _, _) => Task.FromResult(new SpeechToTextResponse("transcript"));

        public int CallCount { get; private set; }

        public int MaxConcurrency { get; private set; }

        public SpeechToTextOptions? LastOptions { get; private set; }

        public async Task<SpeechToTextResponse> GetTextAsync(Stream audioSpeechStream,
            SpeechToTextOptions? options = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastOptions = options;
            var inFlight = Interlocked.Increment(ref _inFlight);
            MaxConcurrency = Math.Max(MaxConcurrency, inFlight);
            try
            {
                return await Responder(audioSpeechStream, options, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }

        public IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(Stream audioSpeechStream,
            SpeechToTextOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    #endregion
}

#pragma warning restore MEAI001
