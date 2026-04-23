namespace CasCap.Models.Dtos;

/// <summary>
/// Response wrapper for the DoorBird info endpoint (<c>info.cgi</c>).
/// </summary>
public record InfoResponse
{
    /// <summary>
    /// The BHA response containing device version and firmware information.
    /// </summary>
    [JsonPropertyName("BHA")]
    public required BHAInfo Bha { get; init; }
}
