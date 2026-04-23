namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a typing indicator event.
/// </summary>
public record SignalTypingMessage
{
    /// <summary>
    /// The action: <c>"STARTED"</c> or <c>"STOPPED"</c>.
    /// </summary>
    [JsonPropertyName("action")]
    public string? Action { get; init; }

    /// <summary>
    /// Unix timestamp in milliseconds.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; init; }
}
