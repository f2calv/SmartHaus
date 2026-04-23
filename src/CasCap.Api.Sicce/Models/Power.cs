namespace CasCap.Models;

/// <summary>Power control settings for a Sicce device.</summary>
public class Power
{
    /// <summary>Power switch state.</summary>
    [JsonPropertyName("power_switch")]
    public bool PowerSwitch { get; set; }
}
