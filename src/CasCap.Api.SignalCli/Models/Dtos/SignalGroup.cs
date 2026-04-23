namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a Signal group returned by the <c>GET /v1/groups/{number}</c> or
/// <c>GET /v1/groups/{number}/{groupId}</c> endpoint.
/// </summary>
public record SignalGroup : INotificationGroup
{
    /// <summary>
    /// The group identifier (e.g. <c>"group.xxx"</c>).
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// The display name of the group.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Optional group description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Phone numbers of current group members.
    /// </summary>
    [JsonPropertyName("members")]
    public string[] Members { get; init; } = [];

    /// <summary>
    /// Phone numbers of group admins.
    /// </summary>
    [JsonPropertyName("admins")]
    public string[] Admins { get; init; } = [];

    /// <summary>
    /// Whether the group is blocked.
    /// </summary>
    [JsonPropertyName("blocked")]
    public bool Blocked { get; init; }

    /// <summary>
    /// The internal group identifier.
    /// </summary>
    [JsonPropertyName("internal_id")]
    public string? InternalId { get; init; }

    /// <summary>
    /// The group invite link.
    /// </summary>
    [JsonPropertyName("invite_link")]
    public string? InviteLink { get; init; }

    /// <summary>
    /// Phone numbers of pending invited members.
    /// </summary>
    [JsonPropertyName("pending_invites")]
    public string[] PendingInvites { get; init; } = [];

    /// <summary>
    /// Phone numbers of members requesting to join.
    /// </summary>
    [JsonPropertyName("pending_requests")]
    public string[] PendingRequests { get; init; } = [];

    /// <summary>
    /// Group permission settings.
    /// </summary>
    [JsonPropertyName("permissions")]
    public GroupPermissions? Permissions { get; init; }
}
