namespace CasCap.Models.Dtos;

/// <summary>
/// Response wrapper for the DoorBird restart endpoint (<c>restart.cgi</c>).
/// </summary>
public record RestartResponse
{
    /// <summary>
    /// The BHA response containing the return code.
    /// </summary>
    [JsonPropertyName("BHA")]
    public required BHAReturnCode Bha { get; init; }
}
