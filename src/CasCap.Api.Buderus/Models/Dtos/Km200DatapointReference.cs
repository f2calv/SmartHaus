namespace CasCap.Models;

/// <summary>Reference to a KM200 datapoint.</summary>
public record Km200DatapointReference
{
    /// <summary>Datapoint identifier.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Datapoint URI path.</summary>
    [JsonPropertyName("uri")]
    public required string Uri { get; init; }

    /// <summary>Returns a string representation of the datapoint reference.</summary>
    /// <returns>A formatted string containing the ID and URI.</returns>
    public override string ToString() => $"{Id} {Uri}";
}
