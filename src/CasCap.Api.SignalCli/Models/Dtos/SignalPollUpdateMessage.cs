namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a poll vote payload within a received <see cref="SignalDataMessage"/>.
/// </summary>
/// <remarks>
/// Present in <see cref="SignalDataMessage.PollVote"/> when a group member casts a
/// vote on an existing poll. Property names match the signal-cli REST API output.
/// </remarks>
public record SignalPollUpdateMessage
{
    /// <summary>The phone number or identifier of the voter.</summary>
    [JsonPropertyName("author")]
    public string? Author { get; init; }

    /// <summary>The E.164 phone number of the voter.</summary>
    [JsonPropertyName("authorNumber")]
    public string? AuthorNumber { get; init; }

    /// <summary>The UUID of the voter.</summary>
    [JsonPropertyName("authorUuid")]
    public string? AuthorUuid { get; init; }

    /// <summary>The sent-timestamp of the original poll message, used as the poll identifier.</summary>
    [JsonPropertyName("targetSentTimestamp")]
    public long? TargetSentTimestamp { get; init; }

    /// <summary>Zero-based indices of the selected answer options.</summary>
    [JsonPropertyName("optionIndexes")]
    public int[]? OptionIndexes { get; init; }

    /// <summary>The total number of votes cast by this voter.</summary>
    [JsonPropertyName("voteCount")]
    public int? VoteCount { get; init; }
}
