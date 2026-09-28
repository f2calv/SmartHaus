namespace CasCap.Models;

/// <summary>A reply produced by an <see cref="ICommsResponder"/> for one <see cref="CommsTurn"/>.</summary>
/// <param name="Text">The answer. It is sent, and spoken when a voice reply applies.</param>
/// <param name="Footer">Text appended to the sent message but never spoken, such as run statistics.</param>
/// <param name="Base64Attachments">Signal data-URI attachments the responder produced.</param>
/// <param name="AfterSendAsync">Invoked after the reply is delivered, for example to post diagnostics.</param>
public sealed record CommsReply(
    string Text,
    string? Footer = null,
    IReadOnlyList<string>? Base64Attachments = null,
    Func<CancellationToken, Task>? AfterSendAsync = null);
