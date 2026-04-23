namespace CasCap.Models.Dtos;

/// <summary>
/// Represents the response from the <c>POST /v1/accounts/{number}/username</c> endpoint.
/// </summary>
public record SetUsernameResponse
{
    /// <summary>
    /// The assigned username.
    /// </summary>
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    /// <summary>
    /// The username link for sharing.
    /// </summary>
    [JsonPropertyName("username_link")]
    public string? UsernameLink { get; init; }
}
