namespace CasCap.Services;

//ITextToSpeechClient is published as experimental (MEAI001). This adapter and its consumers are the
//only contact points, so the diagnostic is suppressed here rather than repository-wide.
#pragma warning disable MEAI001

/// <summary>
/// An <see cref="ITextToSpeechClient"/> implementation targeting Azure AI Speech synthesis.
/// </summary>
/// <remarks>
/// Delegates to <see cref="ISpeechService"/>, which owns the Azure SDK and authenticates with the
/// same <see cref="Azure.Core.TokenCredential"/> as the rest of the application. The adapter carries
/// no Signal types and never logs the text it speaks.
/// </remarks>
public sealed partial class AzureSpeechTextToSpeechClient : ITextToSpeechClient
{
    private readonly ILogger<AzureSpeechTextToSpeechClient> _logger;
    private readonly ISpeechService _speechService;
    private readonly IOptions<TextToSpeechConfig> _options;

    /// <summary>Initializes a new instance of the <see cref="AzureSpeechTextToSpeechClient"/> class.</summary>
    public AzureSpeechTextToSpeechClient(ILogger<AzureSpeechTextToSpeechClient> logger,
        ISpeechService speechService, IOptions<TextToSpeechConfig> options)
    {
        _logger = logger;
        _speechService = speechService;
        _options = options;
    }

    /// <summary>The media type of the audio this adapter produces.</summary>
    /// <remarks>Opus in an Ogg container, which is what messaging clients use for voice notes.</remarks>
    public const string OggOpusMediaType = "audio/ogg";

    /// <inheritdoc/>
    public async Task<TextToSpeechResponse> GetAudioAsync(string text, TextToSpeechOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var voice = options?.VoiceId ?? _options.Value.Voice;
        var audio = await _speechService.SynthesizeAsync(text, voice, cancellationToken);
        if (audio is null || audio.Length == 0)
        {
            LogNoAudioSynthesized(_logger, text.Length);
            return new TextToSpeechResponse();
        }

        return new TextToSpeechResponse([new DataContent(audio, OggOpusMediaType)]);
    }

    /// <inheritdoc/>
    /// <remarks>Synthesis is request-response here, so the completed response is replayed as updates.</remarks>
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
    /// <remarks>The <see cref="ISpeechService"/> is owned by the container.</remarks>
    public void Dispose() { }

    #region Private helpers

    //The character count is safe to log; the text itself is a reply that may quote household detail.
    [LoggerMessage(LogLevel.Warning, "{ClassName} synthesized no audio for {CharacterCount} characters")]
    private static partial void LogNoAudioSynthesized(ILogger logger, int characterCount,
        string className = nameof(AzureSpeechTextToSpeechClient));

    #endregion
}

#pragma warning restore MEAI001
