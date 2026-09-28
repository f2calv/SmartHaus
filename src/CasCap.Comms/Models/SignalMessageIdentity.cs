namespace CasCap.Models;

/// <summary>The stable coordinates that identify one inbound Signal message for duplicate suppression.</summary>
/// <remarks>
/// Deliberately excludes the message body and any attachment identifier so a redelivered envelope
/// hashes to the same value as the original.
/// </remarks>
public sealed record SignalMessageIdentity
{
    /// <summary>The endpoint that delivered the message; the Signalizr gateway for every delivery.</summary>
    public required string Account { get; init; }

    /// <summary>The group or direct conversation the message belongs to.</summary>
    public required string Conversation { get; init; }

    /// <summary>The sender identifier, as reported by the receive envelope.</summary>
    public required string Sender { get; init; }

    /// <summary>The sender-assigned message timestamp, in milliseconds since the Unix epoch.</summary>
    public required long Timestamp { get; init; }
}
