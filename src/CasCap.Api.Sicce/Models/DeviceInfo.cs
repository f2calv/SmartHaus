namespace CasCap.Models;

/// <summary>Sicce device information.</summary>
public class DeviceInfo
{
    /// <summary>Device identifier.</summary>
    [JsonPropertyName("Id")]
    public int id { get; set; }

    /// <summary>Device name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>Device MAC address.</summary>
    [JsonPropertyName("mac")]
    public required string MacAddress { get; set; }

    /// <summary>Device online status.</summary>
    [JsonPropertyName("online")]
    public bool IsOnline { get; set; }

    /// <summary>Device model identifier.</summary>
    [JsonPropertyName("device_model_id")]
    public ModelType DeviceModel { get; set; }

    /// <summary>Power switch status.</summary>
    [JsonPropertyName("power_switch")]
    public bool PowerSwitch { get; set; }

    /// <summary>Device temperature.</summary>
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    /// <summary>Wave mode setting.</summary>
    [JsonPropertyName("mode")]
    public int WaveMode { get; set; }

    /// <summary>
    /// Power range, 5 - 100
    /// </summary>
    [JsonPropertyName("v1")]
    public int Power { get; set; }

    /// <summary>Red LED intensity.</summary>
    [JsonPropertyName("led_r")]
    public int LedRed { get; set; }

    /// <summary>Green LED intensity.</summary>
    [JsonPropertyName("led_g")]
    public int LedGreen { get; set; }

    /// <summary>Blue LED intensity.</summary>
    [JsonPropertyName("led_b")]
    public int LedBlue { get; set; }
}
