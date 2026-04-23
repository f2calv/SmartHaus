namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a Signal contact returned by <c>GET /v1/contacts/{number}</c>.
/// </summary>
public record SignalContact
{
    /// <summary>
    /// The contact's phone number.
    /// </summary>
    [JsonPropertyName("number")]
    public string? Number { get; init; }

    /// <summary>
    /// The contact's display name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// The contact's UUID.
    /// </summary>
    [JsonPropertyName("uuid")]
    public string? Uuid { get; init; }

    /// <summary>
    /// The contact's username.
    /// </summary>
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    /// <summary>
    /// The contact's profile name.
    /// </summary>
    [JsonPropertyName("profile_name")]
    public string? ProfileName { get; init; }

    /// <summary>
    /// The contact's given (first) name.
    /// </summary>
    [JsonPropertyName("given_name")]
    public string? GivenName { get; init; }

    /// <summary>
    /// Whether the contact is blocked.
    /// </summary>
    [JsonPropertyName("blocked")]
    public bool Blocked { get; init; }

    /// <summary>
    /// The message expiration time in seconds, or <c>0</c> if not set.
    /// </summary>
    [JsonPropertyName("message_expiration")]
    public int MessageExpiration { get; init; }

    /// <summary>
    /// The color associated with the contact.
    /// </summary>
    [JsonPropertyName("color")]
    public string? Color { get; init; }

    /// <summary>
    /// A user-set note for the contact.
    /// </summary>
    [JsonPropertyName("note")]
    public string? Note { get; init; }

    /// <summary>
    /// The nickname information for this contact.
    /// </summary>
    [JsonPropertyName("nickname")]
    public Nickname? Nickname { get; init; }

    /// <summary>
    /// The contact's profile information.
    /// </summary>
    [JsonPropertyName("profile")]
    public ContactProfile? Profile { get; init; }
}
