namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a request to update an existing Signal group via <c>PUT /v1/groups/{number}/{groupId}</c>.
/// </summary>
public record UpdateGroupRequest
{
    /// <summary>
    /// New group name. Omit to leave unchanged.
    /// </summary>
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    /// <summary>
    /// New group description. Omit to leave unchanged.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>
    /// Base64-encoded avatar image. Omit to leave unchanged.
    /// </summary>
    [JsonPropertyName("base64_avatar")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Base64Avatar { get; init; }

    /// <summary>
    /// Message expiration time in seconds. Use <c>0</c> to disable.
    /// </summary>
    [JsonPropertyName("expiration_time")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ExpirationTime { get; init; }

    /// <summary>
    /// Group link setting: <c>"enabled"</c>, <c>"enabled-with-approval"</c>, or <c>"disabled"</c>.
    /// </summary>
    [JsonPropertyName("group_link")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GroupLink { get; init; }

    /// <summary>
    /// Group permission settings.
    /// </summary>
    [JsonPropertyName("permissions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GroupPermissions? Permissions { get; init; }
}
