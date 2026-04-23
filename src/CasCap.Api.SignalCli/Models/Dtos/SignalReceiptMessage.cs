namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a delivery or read receipt.
/// </summary>
public record SignalReceiptMessage
{
    /// <summary>
    /// The receipt type: <c>"DELIVERY"</c> or <c>"READ"</c>.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// Timestamps of the messages this receipt acknowledges.
    /// </summary>
    [JsonPropertyName("timestamps")]
    public long[]? Timestamps { get; init; }

    /// <summary>
    /// Unix timestamp in milliseconds when the receipt was generated.
    /// </summary>
    [JsonPropertyName("when")]
    public long? When { get; init; }
}
