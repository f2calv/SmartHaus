namespace CasCap.Models.Dtos;

/// <summary>
/// Represents the envelope of a received Signal message, containing source metadata
/// and the typed message payload.
/// </summary>
public record SignalEnvelope
{
    /// <summary>
    /// The sender's phone number in international format (e.g. <c>"+49151..."</c>).
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }

    /// <summary>
    /// The sender's phone number (alias for <see cref="Source"/>).
    /// </summary>
    [JsonPropertyName("sourceNumber")]
    public string? SourceNumber { get; init; }

    /// <summary>
    /// The sender's UUID.
    /// </summary>
    [JsonPropertyName("sourceUuid")]
    public string? SourceUuid { get; init; }

    /// <summary>
    /// The sender's display name.
    /// </summary>
    [JsonPropertyName("sourceName")]
    public string? SourceName { get; init; }

    /// <summary>
    /// The sender's device identifier.
    /// </summary>
    [JsonPropertyName("sourceDevice")]
    public int? SourceDevice { get; init; }

    /// <summary>
    /// Unix timestamp in milliseconds when the message was sent.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; init; }

    /// <summary>
    /// Present when the envelope contains a regular data message (text, attachments, group).
    /// </summary>
    [JsonPropertyName("dataMessage")]
    public SignalDataMessage? DataMessage { get; init; }

    /// <summary>
    /// Present when the envelope is a sync message from another linked device.
    /// </summary>
    [JsonPropertyName("syncMessage")]
    public SignalSyncMessage? SyncMessage { get; init; }

    /// <summary>
    /// Present when the envelope is a typing indicator event.
    /// </summary>
    [JsonPropertyName("typingMessage")]
    public SignalTypingMessage? TypingMessage { get; init; }

    /// <summary>
    /// Present when the envelope is a delivery/read receipt.
    /// </summary>
    [JsonPropertyName("receiptMessage")]
    public SignalReceiptMessage? ReceiptMessage { get; init; }

    /// <summary>Returns a short human-readable label describing the envelope type.</summary>
    [JsonIgnore]
    public string EnvelopeType => this switch
    {
        { DataMessage: not null } => "data",
        { SyncMessage: not null } => "sync",
        { TypingMessage: not null } => $"typing:{TypingMessage.Action?.ToLowerInvariant() ?? "?"}",
        { ReceiptMessage: not null } => $"receipt:{ReceiptMessage.Type?.ToLowerInvariant() ?? "?"}",
        _ => "unknown",
    };
}
