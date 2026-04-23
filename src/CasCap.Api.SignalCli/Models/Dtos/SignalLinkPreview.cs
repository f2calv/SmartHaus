namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a link preview to attach to a Signal message.
/// </summary>
public record SignalLinkPreview
{
    /// <summary>
    /// The URL for the preview.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    /// <summary>
    /// The title of the preview.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// Optional preview description.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>
    /// Optional base64-encoded thumbnail image.
    /// </summary>
    [JsonPropertyName("base64_thumbnail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Base64Thumbnail { get; init; }
}
