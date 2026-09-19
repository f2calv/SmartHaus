namespace CasCap.Services;

//ITextToSpeechClient is published as experimental (MEAI001). This service and the adapters are the
//only contact points, so the diagnostic is suppressed here rather than repository-wide.
#pragma warning disable MEAI001

/// <summary>
/// Applies the spoken-reply policy to an outbound agent response and returns it as a Signal
/// attachment.
/// </summary>
/// <remarks>
/// The outbound counterpart to <see cref="VoiceMessageTranscriptionService"/>. Synthesis is optional
/// decoration on a reply that has already been composed, so every failure is contained here and
/// reported as "no attachment" rather than propagating and costing the sender their text reply.
/// Provider selection and its rationale are recorded in
/// <see href="https://github.com/f2calv/SmartHaus/issues/82">issue 82</see>.
/// </remarks>
public sealed partial class VoiceReplySynthesisService
{
    private readonly ILogger<VoiceReplySynthesisService> _logger;
    private readonly ITextToSpeechClient _textToSpeechClient;
    private readonly IOptions<TextToSpeechConfig> _options;

    /// <summary>Initializes a new instance of the <see cref="VoiceReplySynthesisService"/> class.</summary>
    public VoiceReplySynthesisService(ILogger<VoiceReplySynthesisService> logger,
        ITextToSpeechClient textToSpeechClient, IOptions<TextToSpeechConfig> options)
    {
        _logger = logger;
        _textToSpeechClient = textToSpeechClient;
        _options = options;
    }

    /// <summary>Filename offered to the recipient for a spoken reply.</summary>
    public const string AttachmentFileName = "reply.ogg";

    /// <summary>
    /// Synthesizes <paramref name="text"/> when the configured policy calls for it.
    /// </summary>
    /// <param name="text">
    /// The reply as the agent composed it. Pass the answer alone, without any diagnostic footer.
    /// </param>
    /// <param name="inboundWasVoice">Whether the message being replied to carried audio.</param>
    /// <param name="cancellationToken">Token observed alongside the configured time budget.</param>
    /// <returns>
    /// A Signal <c>data:</c> attachment, or <see langword="null"/> when policy declines to speak or
    /// synthesis fails.
    /// </returns>
    public async Task<string?> TrySynthesizeAttachmentAsync(string? text, bool inboundWasVoice,
        CancellationToken cancellationToken = default)
    {
        var config = _options.Value;

        if (config.Mode is VoiceReplyMode.Disabled)
            return null;
        if (config.Mode is VoiceReplyMode.MatchInbound && !inboundWasVoice)
            return null;
        if (string.IsNullOrWhiteSpace(text))
            return null;

        //Markdown is read out literally, so structure becomes punctuation before anything is spoken.
        var speakable = SpeechTextNormalizer.ToSpeakable(text);
        if (speakable.Length == 0)
            return null;

        if (speakable.Length > config.MaxCharacters)
        {
            LogReplyTooLong(_logger, speakable.Length, config.MaxCharacters);
            return null;
        }

        try
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(config.TimeoutMs);

            var response = await _textToSpeechClient.GetAudioAsync(speakable, options: null, budget.Token);
            var audio = response.Contents.OfType<DataContent>().FirstOrDefault();
            if (audio is null || audio.Data.Length == 0)
            {
                LogNoAudioReturned(_logger, config.Provider.ToString());
                return null;
            }

            var mediaType = audio.MediaType ?? AzureSpeechTextToSpeechClient.OggOpusMediaType;
            LogReplySynthesized(_logger, config.Provider.ToString(), speakable.Length, audio.Data.Length);
            return $"data:{mediaType};filename={AttachmentFileName};base64,"
                + Convert.ToBase64String(audio.Data.Span);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogSynthesisTimedOut(_logger, config.TimeoutMs);
            return null;
        }
        catch (Exception ex)
        {
            LogSynthesisFailed(_logger, ex, config.Provider.ToString());
            return null;
        }
    }

    #region Private helpers

    //Character and byte counts are safe to log; the reply text itself is not.
    [LoggerMessage(LogLevel.Information,
        "{ClassName} synthesized a spoken reply, provider={Provider}, characters={CharacterCount}, bytes={ByteCount}")]
    private static partial void LogReplySynthesized(ILogger logger, string provider, int characterCount,
        int byteCount, string className = nameof(VoiceReplySynthesisService));

    [LoggerMessage(LogLevel.Debug,
        "{ClassName} reply not spoken, characters={CharacterCount} exceeds MaxCharacters={MaxCharacters}")]
    private static partial void LogReplyTooLong(ILogger logger, int characterCount, int maxCharacters,
        string className = nameof(VoiceReplySynthesisService));

    [LoggerMessage(LogLevel.Warning, "{ClassName} synthesis returned no audio, provider={Provider}")]
    private static partial void LogNoAudioReturned(ILogger logger, string provider,
        string className = nameof(VoiceReplySynthesisService));

    [LoggerMessage(LogLevel.Warning, "{ClassName} synthesis exceeded its {TimeoutMs}ms budget")]
    private static partial void LogSynthesisTimedOut(ILogger logger, int timeoutMs,
        string className = nameof(VoiceReplySynthesisService));

    [LoggerMessage(LogLevel.Error, "{ClassName} synthesis failed, provider={Provider}")]
    private static partial void LogSynthesisFailed(ILogger logger, Exception ex, string provider,
        string className = nameof(VoiceReplySynthesisService));

    #endregion
}

#pragma warning restore MEAI001
