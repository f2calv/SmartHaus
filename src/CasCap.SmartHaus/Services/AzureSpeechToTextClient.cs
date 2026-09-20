using System.ClientModel;

namespace CasCap.Services;

//ISpeechToTextClient is published as experimental (MEAI001). This adapter and its consumers are the
//only contact points, so the diagnostic is suppressed here rather than repository-wide.
#pragma warning disable MEAI001

/// <summary>
/// A <see cref="ISpeechToTextClient"/> implementation targeting the Azure AI Speech fast
/// transcription API.
/// </summary>
/// <remarks>
/// Delegates to <see cref="ISpeechService"/>, which owns the Azure SDK and authenticates with the
/// same <see cref="Azure.Core.TokenCredential"/> as the rest of the application. The adapter carries
/// no Signal types and never logs audio bytes or transcripts. Note that this is the only provider
/// that sends audio outside the local network.
/// </remarks>
public sealed partial class AzureSpeechToTextClient : ISpeechToTextClient
{
    private readonly ILogger<AzureSpeechToTextClient> _logger;
    private readonly ISpeechService _speechService;
    private readonly IOptions<SpeechToTextConfig> _options;

    /// <summary>Initializes a new instance of the <see cref="AzureSpeechToTextClient"/> class.</summary>
    public AzureSpeechToTextClient(ILogger<AzureSpeechToTextClient> logger, ISpeechService speechService,
        IOptions<SpeechToTextConfig> options)
    {
        _logger = logger;
        _speechService = speechService;
        _options = options;
    }

    /// <inheritdoc/>
    public async Task<SpeechToTextResponse> GetTextAsync(Stream audioSpeechStream,
        SpeechToTextOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audioSpeechStream);

        //An explicit locale improves accuracy and latency; with none the multilingual model identifies
        //  the language itself, which is what lets one configuration serve English and German.
        var locales = ResolveLocales(options, _options.Value);
        string? text;
        try
        {
            text = await _speechService.TranscribeAsync(audioSpeechStream, locales, cancellationToken);
        }
        catch (ClientResultException ex)
        {
            //Normalised to the exception the pipeline already handles, so a backend failure reaches the
            //  sender as a rejection instead of aborting the receive loop.
            LogTranscriptionRejected(_logger, ex.Status);
            throw new HttpRequestException(
                $"{nameof(AzureSpeechToTextClient)} transcription request failed, StatusCode={ex.Status}.",
                ex, (HttpStatusCode)ex.Status);
        }

        if (text is null)
            LogNoSpeechRecognized(_logger);

        return new SpeechToTextResponse(text ?? string.Empty)
        {
            ModelId = options?.ModelId ?? _options.Value.ModelId,
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The fast transcription API is request-response, so the single completed response is replayed as
    /// updates.
    /// </remarks>
    public async IAsyncEnumerable<SpeechToTextResponseUpdate> GetStreamingTextAsync(Stream audioSpeechStream,
        SpeechToTextOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetTextAsync(audioSpeechStream, options, cancellationToken);
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
    /// <remarks>The <see cref="ISpeechService"/> is owned by the container.</remarks>
    public void Dispose() { }

    #region Private helpers

    //The configured locales win: Azure rejects a bare language code such as "en" with a 400, and
    //  SpeechToTextOptions.SpeechLanguage carries exactly that for the whisper providers.
    private static IReadOnlyList<string>? ResolveLocales(SpeechToTextOptions? speechToTextOptions,
        SpeechToTextConfig config)
    {
        if (config.AzureLocales is { Length: > 0 } locales)
            return locales;
        //A caller-supplied value is only usable when it is a full locale, such as "en-GB".
        var language = speechToTextOptions?.SpeechLanguage;
        return !string.IsNullOrWhiteSpace(language) && language.Contains('-') ? [language] : null;
    }

    [LoggerMessage(LogLevel.Warning, "{ClassName} recognized no speech in the supplied audio")]
    private static partial void LogNoSpeechRecognized(ILogger logger,
        string className = nameof(AzureSpeechToTextClient));

    [LoggerMessage(LogLevel.Error, "{ClassName} transcription request was rejected, StatusCode={StatusCode}")]
    private static partial void LogTranscriptionRejected(ILogger logger, int statusCode,
        string className = nameof(AzureSpeechToTextClient));

    #endregion
}

#pragma warning restore MEAI001
