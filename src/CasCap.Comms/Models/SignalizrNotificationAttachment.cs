namespace CasCap.Models;

/// <summary>Adapts durable Signalizr attachment metadata to the application notification contract.</summary>
internal sealed record SignalizrNotificationAttachment : INotificationAttachment
{
    /// <inheritdoc/>
    public string? Id { get; init; }

    /// <inheritdoc/>
    public string? ContentType { get; init; }
}
