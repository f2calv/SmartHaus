namespace CasCap.Models.Dtos;

/// <summary>
/// Represents the response from the <c>POST /v1/groups/{number}</c> endpoint.
/// </summary>
public record CreateGroupResponse
{
    /// <summary>
    /// The identifier of the newly created group.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }
}
