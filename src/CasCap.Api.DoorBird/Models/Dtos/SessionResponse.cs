namespace CasCap.Models.Dtos;

/// <summary>
/// Response wrapper for the DoorBird session endpoint (<c>getsession.cgi</c>).
/// </summary>
public record SessionResponse
{
    /// <summary>
    /// The BHA response containing session details.
    /// </summary>
    [JsonPropertyName("BHA")]
    public required BHASession Bha { get; init; }
}
