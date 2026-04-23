namespace CasCap.Models.Dtos;

/// <summary>
/// Response wrapper for the DoorBird light-on endpoint (<c>light-on.cgi</c>).
/// </summary>
public record LightOnResponse
{
    /// <summary>
    /// The BHA response containing the return code.
    /// </summary>
    [Description("BHA response containing the API return code.")]
    [JsonPropertyName("BHA")]
    public required BHALightOn Bha { get; init; }
}
