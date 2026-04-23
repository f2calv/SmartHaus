using System.Text.Json.Serialization;

namespace CasCap.Models;

/// <summary>Response from IP discovery service.</summary>
public record FindMyIpResponse
{
    /// <summary>External IP address.</summary>
    [JsonPropertyName("ip")]
    public required string Ip { get; init; }
}
