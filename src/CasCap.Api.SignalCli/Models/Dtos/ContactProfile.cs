namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a contact's profile information within a <see cref="SignalContact"/>.
/// </summary>
public record ContactProfile
{
    /// <summary>
    /// The profile's about/status text.
    /// </summary>
    [JsonPropertyName("about")]
    public string? About { get; init; }

    /// <summary>
    /// The profile's given (first) name.
    /// </summary>
    [JsonPropertyName("given_name")]
    public string? GivenName { get; init; }

    /// <summary>
    /// Whether the profile has an avatar image.
    /// </summary>
    [JsonPropertyName("has_avatar")]
    public bool HasAvatar { get; init; }

    /// <summary>
    /// The Unix timestamp (milliseconds) of the last profile update.
    /// </summary>
    [JsonPropertyName("last_updated_timestamp")]
    public long LastUpdatedTimestamp { get; init; }

    /// <summary>
    /// The profile's last (family) name.
    /// </summary>
    [JsonPropertyName("lastname")]
    public string? Lastname { get; init; }
}
