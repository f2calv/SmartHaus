namespace CasCap.Abstractions;

/// <summary>
/// Produces replies for <see cref="CasCap.Services.CommunicationsBgService"/>, for example by running an AI agent.
/// </summary>
/// <remarks>
/// <para>
/// Optional. Without an available responder the service forwards chat-bound stream events directly
/// and logs inbound messages without replying, reacting or downloading anything.
/// </para>
/// <para>
/// The service owns the transport: duplicate suppression, reactions, typing, voice transcription,
/// the bounded reply queue, spoken replies and delivery. A responder only decides what to say.
/// </para>
/// </remarks>
public interface ICommsResponder
{
    /// <summary>Whether the responder can currently answer; <see langword="false"/> behaves as if none were registered.</summary>
    bool IsAvailable { get; }

    /// <summary>The prompt used for an inbound message that carries an attachment but no text.</summary>
    string DefaultPrompt { get; }

    /// <summary>Turns a chat-bound stream event into a queued turn.</summary>
    /// <param name="commsEvent">The stream event.</param>
    /// <param name="base64Attachments">Media fetched for the event, delivered with the reply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The turn to queue.</returns>
    Task<CommsTurn> CreateStreamTurnAsync(CommsEvent commsEvent, IReadOnlyList<string>? base64Attachments,
        CancellationToken cancellationToken);

    /// <summary>Handles a poll vote.</summary>
    /// <param name="voter">The voter's Signal identifier. Personal data: never log it.</param>
    /// <param name="pollVote">The vote.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A turn to queue, or <see langword="null"/> when the vote needs no reply.</returns>
    Task<CommsTurn?> HandlePollVoteAsync(string voter, SignalizrPollVote pollVote, CancellationToken cancellationToken);

    /// <summary>Handles inbound text that is a command rather than a prompt, such as a slash command.</summary>
    /// <param name="text">The message text or voice transcript.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome, or <see langword="null"/> when the text is not a command.</returns>
    Task<CommsCommandOutcome?> TryHandleCommandAsync(string text, CancellationToken cancellationToken);

    /// <summary>Produces the reply for one queued turn.</summary>
    /// <param name="turn">The queued turn.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The reply, or <see langword="null"/> when there is nothing to send.</returns>
    Task<CommsReply?> RespondAsync(CommsTurn turn, CancellationToken cancellationToken);
}
