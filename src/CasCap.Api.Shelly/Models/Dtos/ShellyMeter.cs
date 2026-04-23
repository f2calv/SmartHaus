using System.Text.Json.Serialization;

namespace CasCap.Models.Dtos;

/// <summary>
/// Power meter reading within a Shelly device status.
/// </summary>
public record ShellyMeter
{
    /// <summary>Instantaneous power consumption in Watts.</summary>
    [JsonPropertyName("power")]
    public double Power { get; init; }

    /// <summary>Whether the power reading is valid.</summary>
    [JsonPropertyName("is_valid")]
    public bool IsValid { get; init; }

    /// <summary>Total energy consumed in Watt-minutes.</summary>
    [JsonPropertyName("total")]
    public double Total { get; init; }
}
