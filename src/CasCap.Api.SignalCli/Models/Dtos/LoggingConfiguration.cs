namespace CasCap.Models.Dtos;

/// <summary>
/// Represents the logging section of the signal-cli configuration.
/// </summary>
public record LoggingConfiguration
{
    /// <summary>
    /// The logging level (e.g. <c>"info"</c>, <c>"debug"</c>, <c>"warn"</c>).
    /// </summary>
    [JsonPropertyName("level")]
    public string? Level { get; init; }
}
