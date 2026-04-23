namespace CasCap.Models.Dtos;

/// <summary>
/// BHA response containing device version and firmware information.
/// </summary>
public record BHAInfo
{
    /// <summary>
    /// The API return code.
    /// </summary>
    [Description("The API return code.")]
    [JsonPropertyName("RETURNCODE")]
    public required string ReturnCode { get; init; }

    /// <summary>
    /// Array of version information entries for the device.
    /// </summary>
    [Description("Array of version information entries for the device.")]
    [JsonPropertyName("VERSION")]
    public required DoorBirdVersion[] Version { get; init; }
}
