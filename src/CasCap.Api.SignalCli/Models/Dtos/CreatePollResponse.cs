namespace CasCap.Models.Dtos;

/// <summary>
/// Represents the response from the <c>POST /v1/polls/{number}</c> endpoint.
/// </summary>
public record CreatePollResponse
{
    /// <summary>
    /// The timestamp identifier of the created poll.
    /// </summary>
    [Description("Timestamp identifier of the created poll. Use this value to close the poll later.")]
    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }
}
