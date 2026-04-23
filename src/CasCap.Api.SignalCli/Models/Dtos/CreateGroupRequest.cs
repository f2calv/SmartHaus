namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a request to create a new Signal group via <c>POST /v1/groups/{number}</c>.
/// </summary>
public record CreateGroupRequest
{
    /// <summary>
    /// The group name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Optional group description.
    /// </summary>
    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>
    /// Phone numbers of the initial group members.
    /// </summary>
    [JsonPropertyName("members")]
    public required string[] Members { get; init; }

    /// <summary>
    /// Optional message expiration time in seconds.
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
