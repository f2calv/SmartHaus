using System.Text.Json.Serialization;

namespace CasCap.Models.Dtos;

/// <summary>
/// The inner device status payload from the Shelly Cloud API.
/// </summary>
public record ShellyDeviceStatus
{
    /// <summary>Array of relay states.</summary>
    [JsonPropertyName("relays")]
    public ShellyRelay[]? Relays { get; init; }

    /// <summary>Array of power meter readings.</summary>
    [JsonPropertyName("meters")]
    public ShellyMeter[]? Meters { get; init; }

    /// <summary>Internal device temperature in Celsius.</summary>
    [JsonPropertyName("temperature")]
    public double Temperature { get; init; }

    /// <summary>Whether an overtemperature condition is active.</summary>
    [JsonPropertyName("overtemperature")]
    public bool Overtemperature { get; init; }
}
