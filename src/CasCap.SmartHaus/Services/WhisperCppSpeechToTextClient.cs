using System.Net.Http.Headers;

namespace CasCap.Services;

//ISpeechToTextClient is published as experimental (MEAI001). This adapter and its consumers are the
//only contact points, so the diagnostic is suppressed here rather than repository-wide.
#pragma warning disable MEAI001

/// <summary>
/// A <see cref="ISpeechToTextClient"/> implementation targeting a whisper.cpp <c>whisper-server</c>
/// deployment.
/// </summary>
/// <remarks>
/// Audio is posted as multipart <c>POST {WhisperCppEndpoint}/inference</c> with a
/// <see cref="AudioFilePartName"/> part. The route, the part name and the form-encoded options all
/// differ from openai-whisper-asr-webservice, which is why this is a separate adapter rather than a
/// mode of <see cref="WhisperAsrSpeechToTextClient"/>. The adapter carries no Signal types and no
/// deployment coordinates, and never logs multipart bodies, part filenames, audio bytes or transcripts.
/// </remarks>
public sealed partial class WhisperCppSpeechToTextClient : HttpClientBase, ISpeechToTextClient
{
    private readonly IOptions<SpeechToTextConfig> _options;

    /// <summary>Initializes a new instance of the <see cref="WhisperCppSpeechToTextClient"/> class.</summary>
    public WhisperCppSpeechToTextClient(ILogger<WhisperCppSpeechToTextClient> logger,
        IOptions<SpeechToTextConfig> options, IHttpClientFactory httpClientFactory)
        : base(logger, httpClientFactory.CreateClient(HttpClientName))
    {
        _options = options;
    }

    /// <summary>The named <see cref="HttpClient"/> registration this adapter resolves.</summary>
    public const string HttpClientName = nameof(WhisperCppSpeechToTextClient);

    /// <summary>The transcription route appended to <see cref="SpeechToTextConfig.WhisperCppEndpoint"/>.</summary>
    public const string RequestPath = "/inference";

    /// <summary>The multipart field name the server reads the audio from.</summary>
    /// <remarks>whisper-server names this <c>file</c>, where openai-whisper-asr-webservice uses <c>audio_file</c>.</remarks>
    public const string AudioFilePartName = "file";

    /// <summary>The MIME type of the normalised audio this adapter expects.</summary>
    public const string WavMediaType = "audio/wav";

    /// <inheritdoc/>
    public async Task<SpeechToTextResponse> GetTextAsync(Stream audioSpeechStream,
        SpeechToTextOptions? speechToTextOptions = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioSpeechStream);

        var endpoint = _options.Value.WhisperCppEndpoint
            ?? throw new InvalidOperationException(
                $"{nameof(SpeechToTextConfig)}.{nameof(SpeechToTextConfig.WhisperCppEndpoint)} is required when " +
                $"{nameof(SpeechToTextProvider.WhisperCpp)} is the selected provider.");
        var language = speechToTextOptions?.SpeechLanguage ?? _options.Value.Language;

        using var content = new MultipartFormDataContent();
        var file = new StreamContent(audioSpeechStream);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(WavMediaType);
        //The server sniffs the container from the part filename, so a synthetic name is used and no
        //  sender-supplied or local filename ever reaches the backend.
        content.Add(file, AudioFilePartName, "audio.wav");
        content.Add(new StringContent("json"), "response_format");
        content.Add(new StringContent(language), "language");

        var (result, _, statusCode, _) = await PostMultipart<TranscriptionResponse, string>(
            $"{endpoint.TrimEnd('/')}{RequestPath}", content, cancellationToken: cancellationToken);

        if (result is null)
        {
            var status = (int)statusCode;
            if (WhisperAsrSpeechToTextClient.IsTransientStatusCode(statusCode))
                LogTranscriptionUnavailable(_logger, status);
            else
                LogTranscriptionRejected(_logger, status);
            throw new HttpRequestException(
                $"{nameof(WhisperCppSpeechToTextClient)} transcription request failed, StatusCode={status}.",
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
    /// The server exposes no streaming route, so the single completed response is replayed as updates.
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

    /// <inheritdoc/>
    /// <remarks>The <see cref="HttpClient"/> is owned by <see cref="IHttpClientFactory"/>.</remarks>
    public void Dispose() { }

    #region Private helpers

    [LoggerMessage(LogLevel.Error, "{ClassName} transcription request was rejected, StatusCode={StatusCode}")]
    private static partial void LogTranscriptionRejected(ILogger logger, int statusCode,
        string className = nameof(WhisperCppSpeechToTextClient));

    [LoggerMessage(LogLevel.Warning, "{ClassName} transcription backend is unavailable, StatusCode={StatusCode}")]
    private static partial void LogTranscriptionUnavailable(ILogger logger, int statusCode,
        string className = nameof(WhisperCppSpeechToTextClient));

    #endregion
}

#pragma warning restore MEAI001
