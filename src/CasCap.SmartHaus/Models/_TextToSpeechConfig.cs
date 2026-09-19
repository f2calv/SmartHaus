namespace CasCap.Models;

/// <summary>Configuration for the text-to-speech boundary and the spoken-reply policy.</summary>
/// <remarks>
/// Bound from the <c>CasCap:TextToSpeechConfig</c> section under the application configuration root.
/// Every property carries a safe default so the section may be absent entirely. The provider-specific
/// properties are nullable and are read only when that provider is selected.
/// </remarks>
public sealed record TextToSpeechConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(TextToSpeechConfig)}";

    /// <inheritdoc cref="VoiceReplyMode" path="/summary"/>
    /// <remarks>
    /// Defaults to <see cref="VoiceReplyMode.Disabled"/> so replies stay text-only until spoken
    /// replies are deliberately enabled.
    /// </remarks>
    public VoiceReplyMode Mode { get; init; } = VoiceReplyMode.Disabled;

    /// <inheritdoc cref="TextToSpeechProvider" path="/summary"/>
    public TextToSpeechProvider Provider { get; init; } = TextToSpeechProvider.AzureSpeech;

    /// <summary>Resource endpoint of the Azure AI Speech account.</summary>
    /// <remarks>
    /// Required only when <see cref="Provider"/> is <see cref="TextToSpeechProvider.AzureSpeech"/>.
    /// Authentication uses the ambient token credential, so no key is held here.
    /// </remarks>
    [Url]
    public string? AzureSpeechEndpoint { get; init; }

    /// <summary>Endpoint of the Azure OpenAI account hosting the audio deployment.</summary>
    /// <remarks>
    /// Required only when <see cref="Provider"/> is <see cref="TextToSpeechProvider.AzureOpenAi"/>.
    /// The audio deployment may live in a different region from the Speech account.
    /// </remarks>
    [Url]
    public string? AzureOpenAiEndpoint { get; init; }

    /// <summary>Name of the Azure OpenAI audio deployment, such as <c>gpt-4o-mini-tts</c>.</summary>
    /// <remarks>Required only when <see cref="Provider"/> is <see cref="TextToSpeechProvider.AzureOpenAi"/>.</remarks>
    public string? AzureOpenAiDeployment { get; init; }

    /// <summary>API version sent with the Azure OpenAI speech request.</summary>
    /// <remarks>
    /// Configurable because audio deployments have so far only been reachable on preview versions,
    /// which are retired on a schedule; moving to the next one should not need a rebuild.
    /// </remarks>
    public string AzureOpenAiApiVersion { get; init; } = "2025-03-01-preview";

    /// <summary>Voice used by <see cref="TextToSpeechProvider.AzureSpeech"/>.</summary>
    /// <remarks>
    /// A full Azure AI Speech voice name, such as <c>en-GB-SoniaNeural</c>. Defaults to
    /// <see langword="null"/>, which leaves the provider's own default.
    /// <para>
    /// TODO: only this voice has been listened to, and it was kept because it is neutral and
    /// unaccented. The <c>en-GB</c> catalogue holds a dozen or so alternatives worth comparing
    /// side by side before settling.
    /// </para>
    /// </remarks>
    public string? AzureSpeechVoice { get; init; }

    /// <summary>Voice used by <see cref="TextToSpeechProvider.AzureOpenAi"/>.</summary>
    /// <remarks>
    /// A short OpenAI voice name such as <c>alloy</c>, <c>sage</c> or <c>coral</c>. Kept separate from
    /// <see cref="AzureSpeechVoice"/> because the two naming schemes are not interchangeable and the
    /// service rejects the wrong one outright.
    /// <para>
    /// TODO: every voice in this set sounds American, and there are no locale variants, so changing
    /// the name will not change the accent. The <c>gpt-4o-mini-tts</c> deployment accepts an
    /// <c>instructions</c> field that steers delivery in natural language, which is the only route to
    /// a British-sounding result here; it is untested.
    /// </para>
    /// </remarks>
    public string? AzureOpenAiVoice { get; init; }

    /// <summary>Endpoint of the Piper server, as <c>host:port</c>.</summary>
    /// <remarks>
    /// Required only when <see cref="Provider"/> is <see cref="TextToSpeechProvider.Piper"/>. This is
    /// a Wyoming protocol socket rather than an HTTP endpoint, so it carries no scheme; the default
    /// port is 10200.
    /// </remarks>
    public string? PiperEndpoint { get; init; }

    /// <summary>Voice used by <see cref="TextToSpeechProvider.Piper"/>.</summary>
    /// <remarks>
    /// A Piper voice name such as <c>en_GB-alba-medium</c>. Defaults to <see langword="null"/>, which
    /// leaves the voice the server was started with.
    /// </remarks>
    public string? PiperVoice { get; init; }

    /// <summary>Path to the ffmpeg executable used to encode raw PCM.</summary>
    /// <remarks>Needed only by providers that emit PCM rather than a compressed container.</remarks>
    public string FfmpegPath { get; init; } = "ffmpeg";

    /// <summary>Longest reply, in characters, that will be synthesized.</summary>
    /// <remarks>
    /// Defaults to <c>1000</c>. An agent can answer at length, and synthesizing a wall of text wastes
    /// both the quota and the listener's patience, so longer replies stay text only.
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int MaxCharacters { get; init; } = 1_000;

    /// <summary>Total time budget in milliseconds for one synthesis.</summary>
    [Range(1, int.MaxValue)]
    public int TimeoutMs { get; init; } = 60_000;
}
