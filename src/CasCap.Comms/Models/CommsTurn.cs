namespace CasCap.Models;

/// <summary>One unit of work in the reply queue of <see cref="CasCap.Services.CommunicationsBgService"/>.</summary>
/// <param name="Prompt">The text the responder answers: a message, a voice transcript or a stream-event prompt.</param>
/// <param name="BinaryContent">A non-audio inbound attachment passed to the responder, or <see langword="null"/>.</param>
/// <param name="MimeType">The media type of <paramref name="BinaryContent"/>.</param>
/// <param name="Sender">The inbound sender, or <see langword="null"/> for a turn no user started. Personal data: never log it.</param>
/// <param name="Timestamp">The inbound message timestamp used as the reaction target, or <see langword="null"/>.</param>
/// <param name="BypassSession">Whether the responder should answer without its conversation history.</param>
/// <param name="ExtraBase64Attachments">Signal data-URI attachments delivered with the reply, such as stream-event media.</param>
/// <param name="InboundWasVoice">Whether the turn started from a voice message, which a spoken reply may match.</param>
public sealed record CommsTurn(
    string Prompt,
    byte[]? BinaryContent = null,
    string? MimeType = null,
    string? Sender = null,
    long? Timestamp = null,
    bool BypassSession = false,
    IReadOnlyList<string>? ExtraBase64Attachments = null,
    bool InboundWasVoice = false);
