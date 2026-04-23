using System.Text.Json.Serialization;

namespace CasCap.Models.Dtos;

/// <summary>
/// Wraps the <c>device_status</c> object within a Shelly Cloud response.
/// </summary>
public record ShellyDeviceData
{
    /// <summary>Whether the device is currently online.</summary>
    [JsonPropertyName("online")]
    public bool Online { get; init; }

    /// <summary>The raw device status payload.</summary>
    [JsonPropertyName("device_status")]
    public ShellyDeviceStatus? DeviceStatus { get; init; }
}
