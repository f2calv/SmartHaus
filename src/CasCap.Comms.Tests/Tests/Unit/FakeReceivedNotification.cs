namespace CasCap.Tests.Unit;

/// <summary>
/// Minimal <see cref="IReceivedNotification"/> that the fixture converts into a Signalizr delivery
/// to drive the comms receive path.
/// </summary>
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
