namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a poll creation payload within a received <see cref="SignalDataMessage"/>.
/// </summary>
/// <remarks>
/// Present in <see cref="SignalDataMessage.PollMessage"/> when the received message is a poll
/// created by another group member. JSON property names are based on the signal-cli REST API
/// output and may need adjustment after live-API verification.
/// </remarks>
public record SignalPollMessage
{
    /// <summary>
    /// The poll identifier (typically the creation timestamp).
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// The poll question text.
    /// </summary>
    [JsonPropertyName("question")]
    public string? Question { get; init; }

    /// <summary>
    /// The available answer options.
    /// </summary>
    [JsonPropertyName("options")]
    public string[]? Options { get; init; }

    /// <summary>
    /// Whether the poll allows selecting multiple answers.
    /// </summary>
    [JsonPropertyName("allowMultipleSelections")]
    public bool? AllowMultipleSelections { get; init; }
}
