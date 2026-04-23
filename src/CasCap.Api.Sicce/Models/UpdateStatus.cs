namespace CasCap.Models;

/// <summary>
/// device_model_id > 6
/// </summary>
public class UpdateStatus
{
    /// <summary>
    /// Power range 5 - 100
    /// </summary>
    [Required, Range(5, 100)]
    [JsonPropertyName("v1")]
    public int Power { get; set; }

    /// <summary>Wave mode type.</summary>
    [JsonPropertyName("mode")]
    public ModeType Mode { get; set; }
}
