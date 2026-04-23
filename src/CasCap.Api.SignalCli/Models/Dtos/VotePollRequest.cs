namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a request to vote on a Signal poll via <c>POST /v1/polls/{number}/vote</c>.
/// </summary>
public record VotePollRequest
{
    /// <summary>
    /// The author of the poll (phone number or UUID).
    /// </summary>
    [JsonPropertyName("poll_author")]
    public required string PollAuthor { get; init; }

    /// <summary>
    /// The timestamp of the poll to vote on (from <see cref="CreatePollResponse.Timestamp"/>).
    /// </summary>
    [JsonPropertyName("poll_timestamp")]
    public required string PollTimestamp { get; init; }

    /// <summary>
    /// The recipient phone number, username, or group ID.
    /// </summary>
    [JsonPropertyName("recipient")]
    public required string Recipient { get; init; }

    /// <summary>
    /// Zero-based indices of the selected answers.
    /// </summary>
    [JsonPropertyName("selected_answers")]
    public required int[] SelectedAnswers { get; init; }
}
