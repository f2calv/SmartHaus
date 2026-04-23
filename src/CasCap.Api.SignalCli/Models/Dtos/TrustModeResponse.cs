namespace CasCap.Models.Dtos;

/// <summary>
/// Represents the trust mode setting for an account from
/// <c>GET /v1/configuration/{number}/settings</c>.
/// </summary>
public record TrustModeResponse
{
    /// <summary>
    /// The current trust mode (e.g. <c>"always"</c>, <c>"on-first-use"</c>).
    /// </summary>
    [JsonPropertyName("trust_mode")]
    public string? TrustMode { get; init; }
}
