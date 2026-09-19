using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace CasCap.Services;

//ITextToSpeechClient is published as experimental (MEAI001). This adapter and its consumers are the
//only contact points, so the diagnostic is suppressed here rather than repository-wide.
#pragma warning disable MEAI001

/// <summary>
/// An <see cref="ITextToSpeechClient"/> implementation targeting a self-hosted Piper server over the
/// Wyoming protocol.
/// </summary>
/// <remarks>
/// Wyoming is a socket protocol rather than HTTP, so this adapter cannot use
/// <see cref="HttpClientBase"/>. Each event is a newline-terminated JSON header declaring
/// <c>data_length</c> and <c>payload_length</c>, followed by those bytes in turn; synthesis arrives as
/// <c>audio-start</c>, a run of <c>audio-chunk</c> events and finally <c>audio-stop</c>.
/// <para>
/// Piper emits raw PCM, so the audio is encoded to Ogg Opus through ffmpeg here rather than being
/// requested from the server, which keeps the adapter contract identical to the cloud providers.
/// </para>
/// </remarks>
public sealed partial class PiperTextToSpeechClient : ITextToSpeechClient
{
    private readonly ILogger<PiperTextToSpeechClient> _logger;
    private readonly IOptions<TextToSpeechConfig> _options;

    /// <summary>Initializes a new instance of the <see cref="PiperTextToSpeechClient"/> class.</summary>
    public PiperTextToSpeechClient(ILogger<PiperTextToSpeechClient> logger, IOptions<TextToSpeechConfig> options)
    {
        _logger = logger;
        _options = options;
    }

    /// <summary>Port the Wyoming protocol listens on when the endpoint omits one.</summary>
    public const int DefaultPort = 10200;

    /// <summary>The media type this adapter produces.</summary>
    public const string OggOpusMediaType = "audio/ogg";

    /// <inheritdoc/>
    public async Task<TextToSpeechResponse> GetAudioAsync(string text, TextToSpeechOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var config = _options.Value;
        var endpoint = config.PiperEndpoint
            ?? throw new InvalidOperationException(
                $"{nameof(TextToSpeechConfig)}.{nameof(TextToSpeechConfig.PiperEndpoint)} is required when " +
                $"{nameof(TextToSpeechProvider.Piper)} is the selected provider.");

        var (host, port) = ParseEndpoint(endpoint);
        var voice = options?.VoiceId ?? config.PiperVoice;

        var (pcm, rate, width, channels) = await SynthesizeAsync(host, port, text, voice, cancellationToken);
        if (pcm.Length == 0)
        {
            LogNoAudioReturned(_logger, host, port);
            return new TextToSpeechResponse();
        }

        var ogg = await EncodeAsync(pcm, rate, width, channels, config.FfmpegPath, cancellationToken);
        return new TextToSpeechResponse([new DataContent(ogg, OggOpusMediaType)]) { ModelId = voice };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The server streams chunks, but the reply is sent as a single attachment, so the completed
    /// response is replayed as updates.
    /// </remarks>
    public async IAsyncEnumerable<TextToSpeechResponseUpdate> GetStreamingAudioAsync(string text,
        TextToSpeechOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetAudioAsync(text, options, cancellationToken);
        foreach (var update in response.ToTextToSpeechResponseUpdates())
            yield return update;
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    /// <inheritdoc/>
    public void Dispose() { }

    #region Private helpers

    /// <summary>Splits a configured endpoint into its host and port.</summary>
    /// <param name="endpoint">Endpoint as <c>host</c>, <c>host:port</c>, or either with a scheme.</param>
    /// <returns>The host and the port, defaulting to <see cref="DefaultPort"/>.</returns>
    public static (string host, int port) ParseEndpoint(string endpoint)
    {
        //A scheme is accepted but ignored, because operators reach for one out of habit.
        var value = endpoint.Contains("://", StringComparison.Ordinal)
            ? endpoint[(endpoint.IndexOf("://", StringComparison.Ordinal) + 3)..]
            : endpoint;
        value = value.TrimEnd('/');

        var separator = value.LastIndexOf(':');
        if (separator > 0 && int.TryParse(value[(separator + 1)..], out var parsed))
            return (value[..separator], parsed);
        return (value, DefaultPort);
    }

    private async Task<(byte[] pcm, int rate, int width, int channels)> SynthesizeAsync(string host, int port,
        string text, string? voice, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, cancellationToken);
        await using var stream = client.GetStream();

        var data = new Dictionary<string, object> { ["text"] = text };
        if (!string.IsNullOrWhiteSpace(voice))
            data["voice"] = new Dictionary<string, object> { ["name"] = voice };
        var request = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["type"] = "synthesize",
            ["data"] = data,
        });
        await stream.WriteAsync(Encoding.UTF8.GetBytes(request + "\n"), cancellationToken);

        using var pcm = new MemoryStream();
        var rate = 22_050;
        var width = 2;
        var channels = 1;

        while (true)
        {
            var header = await ReadHeaderAsync(stream, cancellationToken);
            if (header is null)
                break;

            using var document = JsonDocument.Parse(header);
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;

            JsonElement? payloadData = null;
            if (root.TryGetProperty("data_length", out var dataLength) && dataLength.ValueKind is JsonValueKind.Number)
            {
                var buffer = new byte[dataLength.GetInt32()];
                await stream.ReadExactlyAsync(buffer, cancellationToken);
                payloadData = JsonDocument.Parse(buffer).RootElement.Clone();
            }
            else if (root.TryGetProperty("data", out var inline))
            {
                payloadData = inline.Clone();
            }

            byte[]? payload = null;
            if (root.TryGetProperty("payload_length", out var payloadLength)
                && payloadLength.ValueKind is JsonValueKind.Number)
            {
                payload = new byte[payloadLength.GetInt32()];
                await stream.ReadExactlyAsync(payload, cancellationToken);
            }

            switch (type)
            {
                case "audio-start" when payloadData is { } start:
                    rate = ReadInt(start, "rate", rate);
                    width = ReadInt(start, "width", width);
                    channels = ReadInt(start, "channels", channels);
                    break;
                case "audio-chunk" when payload is not null:
                    pcm.Write(payload, 0, payload.Length);
                    break;
                case "audio-stop":
                    return (pcm.ToArray(), rate, width, channels);
            }
        }

        return (pcm.ToArray(), rate, width, channels);
    }

    private static int ReadInt(JsonElement element, string name, int fallback) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.Number
            ? value.GetInt32() : fallback;

    //Headers are short and are followed immediately by binary, so no buffered reader can be used.
    private static async Task<string?> ReadHeaderAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        var line = new StringBuilder();
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                return line.Length > 0 ? line.ToString() : null;
            if (buffer[0] == (byte)'\n')
                return line.ToString();
            line.Append((char)buffer[0]);
        }
    }

    private async Task<byte[]> EncodeAsync(byte[] pcm, int rate, int width, int channels, string ffmpegPath,
        CancellationToken cancellationToken)
    {
        string[] arguments =
        [
            "-hide_banner", "-loglevel", "error",
            "-f", width == 2 ? "s16le" : "s32le",
            "-ar", rate.ToString(CultureInfo.InvariantCulture),
            "-ac", channels.ToString(CultureInfo.InvariantCulture),
            "-i", "pipe:0",
            "-c:a", "libopus", "-b:a", "32k",
            "-f", "ogg", "pipe:1"
        ];

        //Length-only capture: ffmpeg diagnostics echo stream metadata, so the text is never retained.
        var result = await ShellExtensions.RunProcessWithStdinAsync(ffmpegPath, arguments, pcm,
            ProcessErrorCapture.Length, cancellationToken);

        //The two failures need telling apart: one is a missing binary, the other a bad stream.
        if (result.ExitCode == -1)
        {
            LogFfmpegUnavailable(_logger, ffmpegPath);
            throw new InvalidOperationException(
                $"{nameof(PiperTextToSpeechClient)} could not start ffmpeg at '{ffmpegPath}'. "
                + $"{nameof(TextToSpeechProvider.Piper)} emits raw PCM and needs it to encode Opus.");
        }
        if (!result.Success || result.Output.Length == 0)
        {
            LogEncodingFailed(_logger, result.ExitCode, result.ErrorLength);
            throw new InvalidOperationException(
                $"{nameof(PiperTextToSpeechClient)} failed to encode {pcm.Length} PCM bytes to Opus, "
                + $"ExitCode={result.ExitCode}. The build may lack libopus.");
        }
        return result.Output;
    }

    [LoggerMessage(LogLevel.Warning, "{ClassName} received no audio from {Host}:{Port}")]
    private static partial void LogNoAudioReturned(ILogger logger, string host, int port,
        string className = nameof(PiperTextToSpeechClient));

    [LoggerMessage(LogLevel.Error, "{ClassName} could not start ffmpeg at {FfmpegPath}")]
    private static partial void LogFfmpegUnavailable(ILogger logger, string ffmpegPath,
        string className = nameof(PiperTextToSpeechClient));

    [LoggerMessage(LogLevel.Error, "{ClassName} Opus encoding failed, ExitCode={ExitCode}, ErrorLength={ErrorLength}")]
    private static partial void LogEncodingFailed(ILogger logger, int exitCode, int errorLength,
        string className = nameof(PiperTextToSpeechClient));

    #endregion
}

#pragma warning restore MEAI001
