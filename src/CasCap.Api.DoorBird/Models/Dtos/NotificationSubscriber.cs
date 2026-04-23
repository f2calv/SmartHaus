namespace CasCap.Models.Dtos;

/// <summary>
/// A single HTTP notification subscriber entry registered on the DoorBird device.
/// </summary>
public record NotificationSubscriber
{
    /// <summary>
    /// The event type this subscriber listens for (e.g. "doorbell", "motionsensor").
    /// </summary>
    [JsonPropertyName("_event")]
    public required string Event { get; init; }

    /// <summary>
    /// The subscriber URL that the DoorBird device will call when the event occurs.
    /// </summary>
    [JsonPropertyName("subscrib")]
    public required string Subscrib { get; init; }

    /// <summary>Subscription URL endpoint.</summary>
    [JsonPropertyName("subscribe")]
    public required string subscribe { get; init; }

    /// <summary>Basic authentication username.</summary>
    [JsonPropertyName("user")]
    public required string user { get; init; }

    /// <summary>Basic authentication password.</summary>
    [JsonPropertyName("password")]
    public required string password { get; init; }

    /// <summary>Minimum delay in seconds between notifications.</summary>
    [JsonPropertyName("relaxation")]
    public required string relaxation { get; init; }
}
