using Azure.Core;

namespace CasCap.Services;

//ITextToSpeechClient is published as experimental (MEAI001). This adapter and its consumers are the
//only contact points, so the diagnostic is suppressed here rather than repository-wide.
#pragma warning disable MEAI001

/// <summary>
/// An <see cref="ITextToSpeechClient"/> implementation targeting an Azure OpenAI audio deployment
/// through the OpenAI-compatible speech route.
/// </summary>
/// <remarks>
/// The response body is audio rather than JSON, which <see cref="HttpClientBase"/> already supports by
/// way of a <see cref="byte"/> array result type, so the request goes through the same helper and the
/// same centralised failure logging as every other call. The route shape is served by
/// OpenAI-compatible self-hosted servers too, so this adapter should extend to those with only an
/// endpoint and an authentication change. The adapter never logs the text it speaks.
/// </remarks>
public sealed partial class AzureOpenAiTextToSpeechClient : HttpClientBase, ITextToSpeechClient
{
    private readonly IOptions<TextToSpeechConfig> _options;
    private readonly IOptions<AzureAuthConfig> _authOptions;

    /// <summary>Initializes a new instance of the <see cref="AzureOpenAiTextToSpeechClient"/> class.</summary>
    public AzureOpenAiTextToSpeechClient(ILogger<AzureOpenAiTextToSpeechClient> logger,
        IOptions<TextToSpeechConfig> options, IOptions<AzureAuthConfig> authOptions,
        IHttpClientFactory httpClientFactory)
        : base(logger, httpClientFactory.CreateClient(HttpClientName))
    {
        _options = options;
        _authOptions = authOptions;
    }

    /// <summary>The named <see cref="HttpClient"/> registration this adapter resolves.</summary>
    public const string HttpClientName = nameof(AzureOpenAiTextToSpeechClient);

    /// <summary>The audio format requested from the deployment.</summary>
    public const string ResponseFormat = "opus";

    /// <summary>The media type the requested <see cref="ResponseFormat"/> arrives as.</summary>
    public const string OggOpusMediaType = "audio/ogg";

    /// <summary>The token scope the audio deployment is reached with.</summary>
    public const string TokenScope = "https://cognitiveservices.azure.com/.default";

    /// <inheritdoc/>
    public async Task<TextToSpeechResponse> GetAudioAsync(string text, TextToSpeechOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var config = _options.Value;
        var endpoint = config.AzureOpenAiEndpoint
            ?? throw new InvalidOperationException(
                $"{nameof(TextToSpeechConfig)}.{nameof(TextToSpeechConfig.AzureOpenAiEndpoint)} is required when " +
                $"{nameof(TextToSpeechProvider.AzureOpenAi)} is the selected provider.");
        var deployment = options?.ModelId ?? config.AzureOpenAiDeployment
            ?? throw new InvalidOperationException(
                $"{nameof(TextToSpeechConfig)}.{nameof(TextToSpeechConfig.AzureOpenAiDeployment)} is required when " +
                $"{nameof(TextToSpeechProvider.AzureOpenAi)} is the selected provider.");
        var credential = _authOptions.Value.TokenCredential
            ?? throw new InvalidOperationException(
                $"{nameof(AzureAuthConfig)}.{nameof(AzureAuthConfig.TokenCredential)} is required when " +
                $"{nameof(TextToSpeechProvider.AzureOpenAi)} is the selected provider.");

        var requestUri = $"{endpoint.TrimEnd('/')}/openai/deployments/{Uri.EscapeDataString(deployment)}" +
            $"/audio/speech?api-version={Uri.EscapeDataString(config.AzureOpenAiApiVersion)}";

        //Built as a dictionary so an unset voice is omitted rather than sent as an explicit null.
        var payload = new Dictionary<string, object>
        {
            ["model"] = deployment,
            ["input"] = text,
            ["response_format"] = ResponseFormat,
        };
        var voice = options?.VoiceId ?? config.AzureOpenAiVoice;
        if (!string.IsNullOrWhiteSpace(voice))
            payload["voice"] = voice;

        //The credential caches and refreshes internally, so a token is requested per call.
        var token = await credential.GetTokenAsync(new TokenRequestContext([TokenScope]), cancellationToken);
        var headers = new List<(string name, string value)> { ("Authorization", $"Bearer {token.Token}") };

        var (audio, error, statusCode, _) = await PostJson<byte[], string>(requestUri, payload,
            additionalHeaders: headers, cancellationToken: cancellationToken);

        if (audio is null)
        {
            var status = (int)statusCode;
            if (WhisperAsrSpeechToTextClient.IsTransientStatusCode(statusCode))
                LogSynthesisUnavailable(_logger, status);
            else
                LogSynthesisRejected(_logger, status);
            //The service reports which parameter it rejected, and a bare status code leaves that
            //  undiagnosable; it describes the request, not the text being spoken.
            var detail = error is { Length: > 0 }
                ? $" {error[..Math.Min(error.Length, 500)]}"
                : string.Empty;
            throw new HttpRequestException(
                $"{nameof(AzureOpenAiTextToSpeechClient)} synthesis request failed, StatusCode={status}.{detail}",
                inner: null, statusCode);
        }

        if (audio.Length == 0)
        {
            LogNoAudioSynthesized(_logger, text.Length);
            return new TextToSpeechResponse();
        }

        return new TextToSpeechResponse([new DataContent(audio, OggOpusMediaType)]) { ModelId = deployment };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The deployment can stream, but the reply is sent as a single attachment, so the completed
    /// response is replayed as updates rather than held open.
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
    /// <remarks>The <see cref="HttpClient"/> is owned by <see cref="IHttpClientFactory"/>.</remarks>
    public void Dispose() { }

    #region Private helpers

    [LoggerMessage(LogLevel.Error, "{ClassName} synthesis request was rejected, StatusCode={StatusCode}")]
    private static partial void LogSynthesisRejected(ILogger logger, int statusCode,
        string className = nameof(AzureOpenAiTextToSpeechClient));

    [LoggerMessage(LogLevel.Warning, "{ClassName} synthesis backend is unavailable, StatusCode={StatusCode}")]
    private static partial void LogSynthesisUnavailable(ILogger logger, int statusCode,
        string className = nameof(AzureOpenAiTextToSpeechClient));

    [LoggerMessage(LogLevel.Warning, "{ClassName} synthesized no audio for {CharacterCount} characters")]
    private static partial void LogNoAudioSynthesized(ILogger logger, int characterCount,
        string className = nameof(AzureOpenAiTextToSpeechClient));

    #endregion
}

#pragma warning restore MEAI001
