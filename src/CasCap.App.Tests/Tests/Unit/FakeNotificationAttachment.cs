namespace CasCap.Tests.Unit;

/// <summary>Minimal <see cref="INotificationAttachment"/> used to build inbound envelopes.</summary>
public sealed class FakeNotificationAttachment : INotificationAttachment
{
    /// <inheritdoc/>
    public string? Id { get; init; }

    /// <inheritdoc/>
    public string? ContentType { get; init; }
}
