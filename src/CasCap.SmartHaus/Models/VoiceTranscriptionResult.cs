namespace CasCap.Models;

/// <summary>The typed outcome of a bounded voice-message transcription attempt.</summary>
/// <remarks>
/// Produced by <see cref="CasCap.Services.VoiceMessageTranscriptionService"/>. <see cref="Text"/> is
/// populated only when <see cref="Outcome"/> is <see cref="VoiceTranscriptionOutcome.Success"/>.
/// </remarks>
public sealed record VoiceTranscriptionResult
{
    /// <inheritdoc cref="VoiceTranscriptionOutcome" path="/summary"/>
    public required VoiceTranscriptionOutcome Outcome { get; init; }

    /// <summary>The normalised transcript, or <see langword="null"/> for any non-success outcome.</summary>
    public string? Text { get; init; }

    /// <summary>Whether a usable transcript was produced.</summary>
    public bool TranscriptAvailable => Outcome is VoiceTranscriptionOutcome.Success;

    /// <summary>Creates a successful result carrying the normalised transcript.</summary>
    /// <param name="text">The normalised, non-empty transcript.</param>
    /// <returns>A successful <see cref="VoiceTranscriptionResult"/>.</returns>
    public static VoiceTranscriptionResult Success(string text) =>
        new() { Outcome = VoiceTranscriptionOutcome.Success, Text = text };

    /// <summary>Creates a result representing a rejected or failed transcription.</summary>
    /// <param name="outcome"><inheritdoc cref="VoiceTranscriptionOutcome" path="/summary"/></param>
    /// <returns>A non-success <see cref="VoiceTranscriptionResult"/> carrying no content.</returns>
    public static VoiceTranscriptionResult Failure(VoiceTranscriptionOutcome outcome) =>
        new() { Outcome = outcome };
}
