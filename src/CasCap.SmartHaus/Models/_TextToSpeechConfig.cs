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

    /// <summary>Voice used for synthesis.</summary>
    /// <remarks>
    /// The naming differs per provider: Azure AI Speech expects a full voice name such as
    /// <c>en-GB-SoniaNeural</c>, while the OpenAI-compatible route expects a short name such as
    /// <c>alloy</c>. Defaults to <see langword="null"/>, which leaves the provider's own default.
    /// </remarks>
    public string? Voice { get; init; }

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
