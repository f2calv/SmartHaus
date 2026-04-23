namespace CasCap.Models.Dtos;

/// <summary>
/// Common BHA response containing only a return code. Used by endpoints that do not return additional data.
/// </summary>
public record BHAReturnCode
{
    /// <summary>
    /// The API return code (e.g. "1" for success).
    /// </summary>
    [Description("The API return code (e.g. \"1\" for success).")]
    [JsonPropertyName("RETURNCODE")]
    public required string ReturnCode { get; init; }
}
