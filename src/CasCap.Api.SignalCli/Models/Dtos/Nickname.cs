namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a contact's nickname details within a <see cref="SignalContact"/>.
/// </summary>
public record Nickname
{
    /// <summary>
    /// The given (first) name portion of the nickname.
    /// </summary>
    [JsonPropertyName("given_name")]
    public string? GivenName { get; init; }

    /// <summary>
    /// The family (last) name portion of the nickname.
    /// </summary>
    [JsonPropertyName("family_name")]
    public string? FamilyName { get; init; }

    /// <summary>
    /// The full nickname.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
