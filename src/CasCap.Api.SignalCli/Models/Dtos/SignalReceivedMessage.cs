namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a single message envelope returned by the <c>GET /v1/receive/{number}</c> endpoint.
/// The signal-cli REST API returns a JSON array of these objects.
/// </summary>
/// <remarks>
/// See <see href="https://bbernhard.github.io/signal-cli-rest-api/"/> for the full API specification.
/// </remarks>
public record SignalReceivedMessage : IReceivedNotification
{
    /// <summary>
    /// The message envelope containing source, timestamp and typed message data.
    /// </summary>
    [JsonPropertyName("envelope")]
    public required SignalEnvelope Envelope { get; init; }

    /// <summary>
    /// The account phone number that received the message.
    /// </summary>
    [JsonPropertyName("account")]
    public string? Account { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    string IReceivedNotification.Sender => Envelope.Source ?? Envelope.SourceNumber ?? "unknown";

    /// <inheritdoc/>
    [JsonIgnore]
    string? IReceivedNotification.GroupId => Envelope.DataMessage?.GroupInfo?.GroupId;

    /// <inheritdoc/>
    [JsonIgnore]
    string? IReceivedNotification.Message => Envelope.DataMessage?.Message;

    /// <inheritdoc/>
    [JsonIgnore]
    bool IReceivedNotification.HasContent => Envelope.DataMessage is not null;

    /// <inheritdoc/>
    [JsonIgnore]
    long? IReceivedNotification.Timestamp => Envelope.DataMessage?.Timestamp;

    /// <inheritdoc/>
    [JsonIgnore]
    IReadOnlyList<INotificationAttachment>? IReceivedNotification.Attachments => Envelope.DataMessage?.Attachments;
}
