using System.Text.Json.Serialization;

namespace CasCap.Models.Dtos;

/// <summary>
/// Wraps the relay control result from the Shelly Cloud API.
/// </summary>
public record ShellyRelayControlData
{
    /// <summary>The device ID that was controlled.</summary>
    [JsonPropertyName("device_id")]
    public string? DeviceId { get; init; }
}
