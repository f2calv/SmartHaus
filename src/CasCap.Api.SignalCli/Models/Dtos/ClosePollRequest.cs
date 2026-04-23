namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a request to close a Signal poll via <c>DELETE /v1/polls/{number}</c>.
/// </summary>
public record ClosePollRequest
{
    /// <summary>
    /// The timestamp of the poll to close (from <see cref="CreatePollResponse.Timestamp"/>).
    /// </summary>
    [JsonPropertyName("poll_timestamp")]
    public required string PollTimestamp { get; init; }

    /// <summary>
    /// The recipient phone number, username, or group ID.
    /// </summary>
    [JsonPropertyName("recipient")]
    public required string Recipient { get; init; }
}
