namespace CasCap.Models.Dtos;

/// <summary>
/// Response wrapper for the DoorBird notification list endpoint (<c>notification.cgi</c>).
/// </summary>
public record NotificationListResponse
{
    /// <summary>
    /// The BHA response containing the return code and notification subscriber list.
    /// </summary>
    [JsonPropertyName("BHA")]
    public required BHANotificationList Bha { get; init; }
}
