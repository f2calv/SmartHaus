namespace CasCap.Models.Dtos;

/// <summary>
/// Represents the response from the <c>DELETE /v1/remote-delete/{number}</c> endpoint.
/// </summary>
public record RemoteDeleteResponse
{
    /// <summary>
    /// The timestamp of the delete operation.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }
}
