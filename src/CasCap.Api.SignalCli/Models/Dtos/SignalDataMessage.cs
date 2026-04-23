namespace CasCap.Models.Dtos;

/// <summary>
/// Represents the data message payload of a received Signal message containing
/// the text body, optional attachments and group information.
/// </summary>
public record SignalDataMessage
{
    /// <summary>
    /// The plaintext message body. May be <see langword="null"/> for attachment-only messages.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// Unix timestamp in milliseconds when the message was sent.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; init; }

    /// <summary>
    /// Message expiration time in seconds. <c>0</c> means no expiration.
    /// </summary>
    [JsonPropertyName("expiresInSeconds")]
    public int? ExpiresInSeconds { get; init; }

    /// <summary>
    /// Attachments included with the message. Each entry contains metadata
    /// and an <see cref="SignalReceivedAttachment.Id"/> that can be used to
    /// download the attachment via <c>GET /v1/attachments/{id}</c>.
    /// </summary>
    [JsonPropertyName("attachments")]
    public SignalReceivedAttachment[]? Attachments { get; init; }

    /// <summary>
    /// Group information when the message was sent to a group.
    /// </summary>
    [JsonPropertyName("groupInfo")]
    public SignalGroupInfo? GroupInfo { get; init; }

    /// <summary>
    /// Present when the message is a poll creation.
    /// </summary>
    /// <remarks>
    /// JSON property name is based on observed signal-cli REST API output and may need
    /// adjustment after live verification.
    /// </remarks>
    [JsonPropertyName("pollMessage")]
    public SignalPollMessage? PollMessage { get; init; }

    /// <summary>Present when the message is a poll vote cast by a group member.</summary>
    [JsonPropertyName("pollVote")]
    public SignalPollUpdateMessage? PollVote { get; init; }

    /// <summary>
    /// Captures any JSON properties not mapped to strongly-typed members.
    /// Useful for diagnosing new or undocumented signal-cli payload fields.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
