namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a request to send a Signal message via the <c>POST /v2/send</c> endpoint.
/// </summary>
/// <remarks>
/// See <see href="https://bbernhard.github.io/signal-cli-rest-api/"/> for the full API specification.
/// </remarks>
public record SignalMessageRequest : INotificationMessage
{
    /// <summary>
    /// Internal tracking identifier, not sent to the API.
    /// </summary>
    [JsonIgnore]
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <inheritdoc/>
    [JsonIgnore]
    string INotificationMessage.Sender => Number;

    /// <summary>
    /// The message text to send.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// Sender's phone number in international format (e.g. <c>"+49151..."</c>).
    /// </summary>
    [JsonPropertyName("number")]
    public required string Number { get; init; }

    /// <summary>
    /// Recipient phone numbers or group IDs.
    /// </summary>
    [JsonPropertyName("recipients")]
    public required string[] Recipients { get; init; }

    /// <summary>
    /// Optional base64-encoded attachments.
    /// </summary>
    /// <remarks>
    /// <para>Supported formats:</para>
    /// <list type="bullet">
    ///   <item><description>Plain base64-encoded data.</description></item>
    ///   <item><description><c>data:MIME-TYPE;base64,BASE64-ENCODED-DATA</c></description></item>
    ///   <item><description><c>data:MIME-TYPE;filename=FILENAME;base64,BASE64-ENCODED-DATA</c></description></item>
    /// </list>
    /// </remarks>
    [JsonPropertyName("base64_attachments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Base64Attachments { get; init; }

    /// <summary>
    /// Text mode for the message. Supported values: <c>"normal"</c>, <c>"styled"</c>.
    /// Defaults to the server-configured <c>DEFAULT_SIGNAL_TEXT_MODE</c>.
    /// </summary>
    [JsonPropertyName("text_mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TextMode { get; init; }

    /// <summary>
    /// Optional link preview to include with the message.
    /// </summary>
    [JsonPropertyName("link_preview")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SignalLinkPreview? LinkPreview { get; init; }

    /// <summary>
    /// Unix timestamp (milliseconds) of the message to edit.
    /// </summary>
    [JsonPropertyName("edit_timestamp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? EditTimestamp { get; init; }

    /// <summary>
    /// Mentions to include in the message.
    /// </summary>
    [JsonPropertyName("mentions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MessageMention[]? Mentions { get; init; }

    /// <summary>
    /// Whether to notify the sender's other devices.
    /// </summary>
    [JsonPropertyName("notify_self")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? NotifySelf { get; init; }

    /// <summary>
    /// The author of the quoted message.
    /// </summary>
    [JsonPropertyName("quote_author")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? QuoteAuthor { get; init; }

    /// <summary>
    /// Mentions within the quoted message.
    /// </summary>
    [JsonPropertyName("quote_mentions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MessageMention[]? QuoteMentions { get; init; }

    /// <summary>
    /// The text of the quoted message.
    /// </summary>
    [JsonPropertyName("quote_message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? QuoteMessage { get; init; }

    /// <summary>
    /// Unix timestamp (milliseconds) of the quoted message.
    /// </summary>
    [JsonPropertyName("quote_timestamp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? QuoteTimestamp { get; init; }

    /// <summary>
    /// Sticker identifier to send.
    /// </summary>
    [JsonPropertyName("sticker")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Sticker { get; init; }

    /// <summary>
    /// Whether the message should be view-once.
    /// </summary>
    [JsonPropertyName("view_once")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ViewOnce { get; init; }
}
