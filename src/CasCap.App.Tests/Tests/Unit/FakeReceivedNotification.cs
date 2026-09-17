namespace CasCap.Tests.Unit;

/// <summary>
/// Minimal <see cref="IReceivedNotification"/> used to drive the comms receive path without the
/// signal-cli envelope shape.
/// </summary>
/// <remarks>
/// Deliberately not a <c>SignalReceivedMessage</c>, so the poll-vote and content-only diagnostic
/// branches stay out of the way of the paths under test.
/// </remarks>
public sealed class FakeReceivedNotification : IReceivedNotification
{
    /// <inheritdoc/>
    public required string Sender { get; init; }

    /// <inheritdoc/>
    public string? GroupId { get; init; }

    /// <inheritdoc/>
    public string? Message { get; init; }

    /// <inheritdoc/>
    public bool HasContent { get; init; } = true;

    /// <inheritdoc/>
    public long? Timestamp { get; init; }

    /// <inheritdoc/>
    public IReadOnlyList<INotificationAttachment>? Attachments { get; init; }
}
