namespace CasCap.Models.Dtos;

/// <summary>
/// Represents group information attached to a received Signal data message.
/// </summary>
public record SignalGroupInfo
{
    /// <summary>
    /// The group identifier.
    /// </summary>
    [JsonPropertyName("groupId")]
    public string? GroupId { get; init; }

    /// <summary>
    /// The type of group event (e.g. <c>"DELIVER"</c>, <c>"UPDATE"</c>).
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }
}
