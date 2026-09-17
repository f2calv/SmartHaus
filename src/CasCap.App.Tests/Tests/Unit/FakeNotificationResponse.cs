namespace CasCap.Tests.Unit;

/// <summary>Minimal <see cref="INotificationResponse"/> returned by <see cref="FakeNotifier"/>.</summary>
public sealed class FakeNotificationResponse : INotificationResponse
{
    /// <inheritdoc/>
    public required string Timestamp { get; init; }
}
