namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a known identity returned by <c>GET /v1/identities/{number}</c>.
/// </summary>
public record SignalIdentity
{
    /// <summary>
    /// The phone number associated with this identity.
    /// </summary>
    [JsonPropertyName("number")]
    public string? Number { get; init; }

    /// <summary>
    /// The UUID of the identity.
    /// </summary>
    [JsonPropertyName("uuid")]
    public string? Uuid { get; init; }

    /// <summary>
    /// The trust status (e.g. <c>"TRUSTED_UNVERIFIED"</c>, <c>"TRUSTED_VERIFIED"</c>, <c>"UNTRUSTED"</c>).
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>
    /// The identity key fingerprint.
    /// </summary>
    [JsonPropertyName("fingerprint")]
    public string? Fingerprint { get; init; }

    /// <summary>
    /// The safety number for verification.
    /// </summary>
    [JsonPropertyName("safety_number")]
    public string? SafetyNumber { get; init; }

    /// <summary>
    /// The date/time string when this identity was first seen.
    /// </summary>
    [JsonPropertyName("added")]
    public string? Added { get; init; }
}
