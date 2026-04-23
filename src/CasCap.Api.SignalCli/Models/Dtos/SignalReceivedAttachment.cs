namespace CasCap.Models.Dtos;

/// <summary>
/// Represents metadata of an attachment included in a received Signal message.
/// </summary>
public record SignalReceivedAttachment : INotificationAttachment
{
    /// <summary>
    /// The MIME content type (e.g. <c>"image/jpeg"</c>, <c>"audio/aac"</c>).
    /// </summary>
    [JsonPropertyName("contentType")]
    public string? ContentType { get; init; }

    /// <summary>
    /// The original filename, if provided by the sender.
    /// </summary>
    [JsonPropertyName("filename")]
    public string? Filename { get; init; }

    /// <summary>
    /// The attachment identifier used to download or delete via
    /// <c>GET /v1/attachments/{id}</c> or <c>DELETE /v1/attachments/{id}</c>.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The size of the attachment in bytes.
    /// </summary>
    [JsonPropertyName("size")]
    public long? Size { get; init; }
}
