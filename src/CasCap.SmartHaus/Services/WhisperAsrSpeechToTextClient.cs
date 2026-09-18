using System.Net.Http.Headers;

namespace CasCap.Services;

//ISpeechToTextClient is published as experimental (MEAI001). This adapter and its consumers are the
//only contact points, so the diagnostic is suppressed here rather than repository-wide.
#pragma warning disable MEAI001

/// <summary>
/// A <see cref="ISpeechToTextClient"/> implementation targeting an openai-whisper-asr-webservice
/// deployment.
/// </summary>
/// <remarks>
/// Audio is posted as multipart <c>POST {Endpoint}/asr</c> with an <see cref="AudioFilePartName"/>
/// part and the <c>task</c>, <c>language</c>, <c>encode</c> and <c>output</c> query parameters that
/// service expects. <c>encode=false</c> is sent for WAV input so the server skips its own ffmpeg
/// pass. The adapter carries no Signal types and no deployment coordinates, and never logs multipart
/// bodies, part filenames, audio bytes or transcripts.
/// </remarks>
public sealed partial class WhisperAsrSpeechToTextClient : HttpClientBase, ISpeechToTextClient
{
    private readonly IOptions<SpeechToTextConfig> _options;

    /// <summary>Initializes a new instance of the <see cref="WhisperAsrSpeechToTextClient"/> class.</summary>
    public WhisperAsrSpeechToTextClient(ILogger<WhisperAsrSpeechToTextClient> logger,
        IOptions<SpeechToTextConfig> options, IHttpClientFactory httpClientFactory)
        : base(logger, httpClientFactory.CreateClient(HttpClientName))
    {
        _options = options;
    }

    /// <summary>The named <see cref="HttpClient"/> registration this adapter resolves.</summary>
    public const string HttpClientName = nameof(WhisperAsrSpeechToTextClient);

    /// <summary>The transcription route appended to <see cref="SpeechToTextConfig.Endpoint"/>.</summary>
    public const string RequestPath = "/asr";

    /// <summary>The multipart field name the service reads the audio from.</summary>
    public const string AudioFilePartName = "audio_file";

    /// <summary>
    /// The <see cref="SpeechToTextOptions.AdditionalProperties"/> key carrying the audio MIME type.
    /// </summary>
    /// <remarks>
    /// <see cref="ISpeechToTextClient"/> supplies only a <see cref="Stream"/>, so the container type
    /// is passed alongside it. Defaults to <see cref="WavMediaType"/> when absent.
    /// </remarks>
    public const string MediaTypePropertyKey = "mediaType";

    /// <summary>The MIME type of the normalised audio this adapter expects.</summary>
    public const string WavMediaType = "audio/wav";

    /// <inheritdoc/>
    public async Task<SpeechToTextResponse> GetTextAsync(Stream audioSpeechStream,
        SpeechToTextOptions? speechToTextOptions = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioSpeechStream);

        var mediaType = ResolveMediaType(speechToTextOptions);
        //encode=false skips the server-side ffmpeg pass; the caller has already produced the WAV it wants.
        var encode = !IsWav(mediaType);
        var language = speechToTextOptions?.SpeechLanguage ?? _options.Value.Language;
        var requestUri =
            $"{_options.Value.Endpoint.TrimEnd('/')}{RequestPath}" +
            $"?task=transcribe&language={Uri.EscapeDataString(language)}" +
            $"&encode={(encode ? "true" : "false")}&output=json";

        using var content = new MultipartFormDataContent();
        var file = new StreamContent(audioSpeechStream);
        if (MediaTypeHeaderValue.TryParse(mediaType, out var parsedMediaType))
            file.Headers.ContentType = parsedMediaType;
        content.Add(file, AudioFilePartName, ToPartFileName(mediaType));

        var (result, _, statusCode, _) = await PostMultipart<TranscriptionResponse, string>(
            requestUri, content, cancellationToken: cancellationToken);

        if (result is null)
        {
            var status = (int)statusCode;
            if (IsTransientStatusCode(statusCode))
                LogTranscriptionUnavailable(_logger, status);
            else
                LogTranscriptionRejected(_logger, status);
            throw new HttpRequestException(
                $"{nameof(WhisperAsrSpeechToTextClient)} transcription request failed, StatusCode={status}.",
                inner: null, statusCode);
        }

        return new SpeechToTextResponse(result.Text ?? string.Empty)
        {
            ModelId = speechToTextOptions?.ModelId ?? _options.Value.ModelId,
            RawRepresentation = result,
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The service exposes no streaming route, so the single completed response is replayed as
    /// updates.
    /// </remarks>
    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(Stream audioSpeechStream,
        SpeechToTextOptions? speechToTextOptions = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetTextAsync(audioSpeechStream, speechToTextOptions, cancellationToken);
        foreach (var update in response.ToSpeechToTextResponseUpdates())
            yield return update;
    }

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    /// <summary>Whether a non-success status code is worth retrying.</summary>
    /// <param name="statusCode">The status code returned by the transcription backend.</param>
    /// <returns><see langword="true"/> for timeout, throttling and server-side failures.</returns>
    public static bool IsTransientStatusCode(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or >= HttpStatusCode.InternalServerError;

    /// <inheritdoc/>
    /// <remarks>The <see cref="HttpClient"/> is owned by <see cref="IHttpClientFactory"/>.</remarks>
    public void Dispose() { }

    #region Private helpers

    private static string ResolveMediaType(SpeechToTextOptions? speechToTextOptions) =>
        speechToTextOptions?.AdditionalProperties?.TryGetValue(MediaTypePropertyKey, out var value) is true
        && value is string mediaType && !string.IsNullOrWhiteSpace(mediaType)
            ? mediaType
            : WavMediaType;

    private static bool IsWav(string mediaType) =>
        mediaType is WavMediaType or "audio/x-wav" or "audio/wave" or "audio/vnd.wave";

    //The service sniffs the container from the part filename, so a synthetic name is used and no
    //sender-supplied or local filename ever reaches the backend.
    private static string ToPartFileName(string mediaType) => mediaType switch
    {
        WavMediaType or "audio/x-wav" or "audio/wave" or "audio/vnd.wave" => "audio.wav",
        "audio/aac" => "audio.aac",
        "audio/mpeg" or "audio/mp3" => "audio.mp3",
        "audio/mp4" or "audio/m4a" or "audio/x-m4a" => "audio.m4a",
        "audio/ogg" or "audio/oga" => "audio.ogg",
        "audio/opus" => "audio.opus",
        "audio/flac" or "audio/x-flac" => "audio.flac",
        _ => "audio.bin"
    };

    [LoggerMessage(LogLevel.Error, "{ClassName} transcription request was rejected, StatusCode={StatusCode}")]
    private static partial void LogTranscriptionRejected(ILogger logger, int statusCode,
        string className = nameof(WhisperAsrSpeechToTextClient));

    [LoggerMessage(LogLevel.Warning, "{ClassName} transcription backend is unavailable, StatusCode={StatusCode}")]
    private static partial void LogTranscriptionUnavailable(ILogger logger, int statusCode,
        string className = nameof(WhisperAsrSpeechToTextClient));

    #endregion
}

#pragma warning restore MEAI001
