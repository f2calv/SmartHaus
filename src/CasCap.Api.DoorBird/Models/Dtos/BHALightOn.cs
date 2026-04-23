namespace CasCap.Models.Dtos;

/// <summary>
/// BHA response for the light-on endpoint.
/// </summary>
public record BHALightOn
{
    /// <summary>
    /// The API return code (e.g. "1" for success).
    /// </summary>
    [Description("The API return code (e.g. \"1\" for success).")]
    [JsonPropertyName("RETURNCODE")]
    public required string ReturnCode { get; init; }
}
