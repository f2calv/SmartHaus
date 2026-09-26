namespace CasCap.Models;

/// <summary>Adapts a durable Signalizr delivery to the application notification contract.</summary>
internal sealed record SignalizrReceivedNotification : IReceivedNotification
{
    /// <inheritdoc/>
    public required string Sender { get; init; }

    /// <inheritdoc/>
    public string? GroupId { get; init; }

    /// <inheritdoc/>
    public string? Message { get; init; }

    /// <inheritdoc/>
    public bool HasContent => !string.IsNullOrWhiteSpace(Message) || Attachments is { Count: > 0 } || PollVote is not null;

    /// <inheritdoc/>
    public long? Timestamp { get; init; }

    /// <inheritdoc/>
    public IReadOnlyList<INotificationAttachment>? Attachments { get; init; }

    /// <summary>Whether the gateway's own account sent the message.</summary>
    public bool FromSelf { get; init; }

    /// <summary>The poll vote the delivery carries, or <see langword="null"/>.</summary>
    public SignalizrPollVote? PollVote { get; init; }

    /// <summary>Creates an application notification from a Signalizr delivery.</summary>
    public static SignalizrReceivedNotification From(SignalizrMessage message) =>
        new()
        {
            Sender = message.Sender ?? string.Empty,
            GroupId = message.Channel,
            // The wire format has no null string, so an attachment-only delivery arrives empty.
            Message = string.IsNullOrEmpty(message.Message) ? null : message.Message,
            Timestamp = message.Timestamp == 0 ? null : message.Timestamp,
            Attachments = message.Attachments.Count == 0
                ? null
                : [.. message.Attachments.Select(attachment => new SignalizrNotificationAttachment
                {
                    Id = attachment.Id,
                    ContentType = attachment.ContentType,
                })],
            FromSelf = message.FromSelf,
            PollVote = message.PollVote,
        };
}
