namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a linked device returned by <c>GET /v1/devices/{number}</c>.
/// </summary>
public record SignalDevice
{
    /// <summary>
    /// The device identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    /// <summary>
    /// The display name of the device.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// The Unix timestamp (milliseconds) when the device was created.
    /// </summary>
    [JsonPropertyName("creation_timestamp")]
    public required long CreationTimestamp { get; init; }

    /// <summary>
    /// The Unix timestamp (milliseconds) of the device's last activity.
    /// </summary>
    [JsonPropertyName("last_seen_timestamp")]
    public required long LastSeenTimestamp { get; init; }
}
