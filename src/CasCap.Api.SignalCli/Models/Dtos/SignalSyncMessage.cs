namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a sync message from another linked device.
/// </summary>
public record SignalSyncMessage
{
    /// <summary>
    /// The data message that was synced from another device.
    /// </summary>
    [JsonPropertyName("sentMessage")]
    public SignalDataMessage? SentMessage { get; init; }
}
