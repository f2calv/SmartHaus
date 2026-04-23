namespace CasCap.Models.Dtos;

/// <summary>
/// Represents the response from the <c>GET /v1/about</c> endpoint.
/// </summary>
public record SignalAbout
{
    /// <summary>
    /// The build number of the running signal-cli-rest-api instance.
    /// </summary>
    [JsonPropertyName("build")]
    public required int Build { get; init; }

    /// <summary>
    /// The version string of the running signal-cli-rest-api instance.
    /// </summary>
    [JsonPropertyName("version")]
    public required string Version { get; init; }

    /// <summary>
    /// The list of supported API versions.
    /// </summary>
    [JsonPropertyName("versions")]
    public string[] Versions { get; init; } = [];

    /// <summary>
    /// The execution mode: <c>"normal"</c>, <c>"native"</c>, or <c>"json-rpc"</c>.
    /// </summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    /// <summary>
    /// Server capability flags.
    /// </summary>
    [JsonPropertyName("capabilities")]
    public Dictionary<string, object>? Capabilities { get; init; }
}
