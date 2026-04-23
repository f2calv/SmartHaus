using System.Text.Json.Serialization;

namespace CasCap.Models.Dtos;

/// <summary>
/// Response from the Shelly Cloud <c>/device/relay/control</c> endpoint.
/// </summary>
public record ShellyRelayControlResponse
{
    /// <summary>Whether the API call was successful.</summary>
    [JsonPropertyName("isok")]
    public bool IsOk { get; init; }

    /// <summary>The nested relay control result.</summary>
    [JsonPropertyName("data")]
    public ShellyRelayControlData? Data { get; init; }
}
