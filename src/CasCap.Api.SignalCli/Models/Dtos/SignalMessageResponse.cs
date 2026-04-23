namespace CasCap.Models.Dtos;

/// <summary>
/// Represents the response from the <c>POST /v2/send</c> endpoint.
/// </summary>
public record SignalMessageResponse : INotificationResponse
{
    /// <summary>
    /// The message timestamp returned by the Signal server.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }
}
