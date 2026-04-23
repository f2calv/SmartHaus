namespace CasCap.Models.Dtos;

/// <summary>
/// Represents the signal-cli configuration from <c>GET /v1/configuration</c>.
/// </summary>
public record SignalConfiguration
{
    /// <summary>
    /// The logging configuration section.
    /// </summary>
    [JsonPropertyName("logging")]
    public LoggingConfiguration? Logging { get; init; }
}
