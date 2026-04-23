namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a request to create a Signal poll via <c>POST /v1/polls/{number}</c>.
/// </summary>
public record CreatePollRequest
{
    /// <summary>
    /// The poll question text.
    /// </summary>
    [JsonPropertyName("question")]
    public required string Question { get; init; }

    /// <summary>
    /// The list of answer options.
    /// </summary>
    [JsonPropertyName("answers")]
    public required string[] Answers { get; init; }

    /// <summary>
    /// The recipient phone number, username, or group ID.
    /// </summary>
    [JsonPropertyName("recipient")]
    public required string Recipient { get; init; }

    /// <summary>
    /// Whether the poll allows selecting multiple answers.
    /// </summary>
    [JsonPropertyName("allow_multiple_selections")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AllowMultipleSelections { get; init; }
}
