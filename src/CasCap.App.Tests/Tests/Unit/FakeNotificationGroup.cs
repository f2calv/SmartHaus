namespace CasCap.Tests.Unit;

/// <summary>Minimal <see cref="INotificationGroup"/> returned by <see cref="FakeNotifier"/>.</summary>
public sealed class FakeNotificationGroup : INotificationGroup
{
    /// <inheritdoc/>
    public required string Id { get; init; }

    /// <inheritdoc/>
    public required string Name { get; init; }

    /// <inheritdoc/>
    public string[] Members { get; init; } = [];
}
