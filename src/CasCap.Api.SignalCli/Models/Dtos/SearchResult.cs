namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a single result from the <c>GET /v1/search/{number}</c> endpoint.
/// </summary>
public record SearchResult
{
    /// <summary>
    /// The phone number that was searched.
    /// </summary>
    [JsonPropertyName("number")]
    public string? Number { get; init; }

    /// <summary>
    /// Whether the number is registered on Signal.
    /// </summary>
    [JsonPropertyName("registered")]
    public bool Registered { get; init; }
}
