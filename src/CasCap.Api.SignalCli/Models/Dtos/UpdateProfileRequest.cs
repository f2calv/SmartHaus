namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a request to update a Signal profile via <c>PUT /v1/profiles/{number}</c>.
/// </summary>
public record UpdateProfileRequest
{
    /// <summary>
    /// New display name. Omit to leave unchanged.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    /// <summary>
    /// Base64-encoded avatar image. Omit to leave unchanged.
    /// </summary>
    [JsonPropertyName("base64_avatar")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Base64Avatar { get; init; }

    /// <summary>
    /// About/bio text. Omit to leave unchanged.
    /// </summary>
    [JsonPropertyName("about")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? About { get; init; }
}
