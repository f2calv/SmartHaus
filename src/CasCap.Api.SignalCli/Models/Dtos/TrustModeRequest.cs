namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a request to set the trust mode for an account via
/// <c>POST /v1/configuration/{number}/settings</c>.
/// </summary>
public record TrustModeRequest
{
    /// <summary>
    /// The desired trust mode (e.g. <c>"always"</c>, <c>"on-first-use"</c>).
    /// </summary>
    [JsonPropertyName("trust_mode")]
    public required string TrustMode { get; init; }
}
