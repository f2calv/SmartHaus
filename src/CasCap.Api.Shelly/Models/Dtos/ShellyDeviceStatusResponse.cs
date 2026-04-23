using System.Text.Json.Serialization;

namespace CasCap.Models.Dtos;

/// <summary>
/// Response from the Shelly Cloud <c>/device/status</c> endpoint.
/// </summary>
public record ShellyDeviceStatusResponse
{
    /// <summary>Whether the API call was successful.</summary>
    [JsonPropertyName("isok")]
    public bool IsOk { get; init; }

    /// <summary>The nested device data payload.</summary>
    [JsonPropertyName("data")]
    public ShellyDeviceData? Data { get; init; }
}
