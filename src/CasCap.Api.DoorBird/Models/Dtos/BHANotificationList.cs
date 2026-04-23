namespace CasCap.Models.Dtos;

/// <summary>
/// BHA response containing the list of HTTP notification subscribers.
/// </summary>
public record BHANotificationList
{
    /// <summary>
    /// The API return code.
    /// </summary>
    [Description("The API return code.")]
    [JsonPropertyName("RETURNCODE")]
    public required string ReturnCode { get; init; }

    /// <summary>
    /// The array of notification subscriber entries.
    /// </summary>
    [Description("The array of notification subscriber entries.")]
    [JsonPropertyName("NOTIFICATIONS")]
    public required NotificationSubscriber[] Notifications { get; init; }
}
