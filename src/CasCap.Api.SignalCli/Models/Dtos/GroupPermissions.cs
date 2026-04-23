namespace CasCap.Models.Dtos;

/// <summary>
/// Represents group permission settings for a Signal group.
/// </summary>
public record GroupPermissions
{
    /// <summary>
    /// Who can add members: <c>"every-member"</c> or <c>"only-admins"</c>.
    /// </summary>
    [JsonPropertyName("add_members")]
    public string? AddMembers { get; init; }

    /// <summary>
    /// Who can edit group details: <c>"every-member"</c> or <c>"only-admins"</c>.
    /// </summary>
    [JsonPropertyName("edit_group")]
    public string? EditGroup { get; init; }

    /// <summary>
    /// Who can send messages: <c>"every-member"</c> or <c>"only-admins"</c>.
    /// </summary>
    [JsonPropertyName("send_messages")]
    public string? SendMessages { get; init; }
}
