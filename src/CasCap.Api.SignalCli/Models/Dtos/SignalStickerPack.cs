namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a sticker pack returned by <c>GET /v1/sticker-packs/{number}</c>.
/// </summary>
public record SignalStickerPack
{
    /// <summary>
    /// The unique identifier of the sticker pack.
    /// </summary>
    [JsonPropertyName("pack_id")]
    public string? PackId { get; init; }

    /// <summary>
    /// The title of the sticker pack.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// The author of the sticker pack.
    /// </summary>
    [JsonPropertyName("author")]
    public string? Author { get; init; }

    /// <summary>
    /// The URL of the sticker pack.
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    /// <summary>
    /// Whether the sticker pack is installed.
    /// </summary>
    [JsonPropertyName("installed")]
    public bool Installed { get; init; }
}
