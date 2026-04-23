namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a mention within a Signal message.
/// </summary>
public record MessageMention
{
    /// <summary>
    /// The phone number or UUID of the mentioned user.
    /// </summary>
    [JsonPropertyName("author")]
    public required string Author { get; init; }

    /// <summary>
    /// The start position of the mention in the message text.
    /// </summary>
    [JsonPropertyName("start")]
    public required int Start { get; init; }

    /// <summary>
    /// The length of the mention in the message text.
    /// </summary>
    [JsonPropertyName("length")]
    public required int Length { get; init; }
}
