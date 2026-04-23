namespace CasCap.Models.Dtos;

/// <summary>
/// Remote control status of a Miele appliance.
/// </summary>
public record RemoteEnable
{
    /// <summary>
    /// Whether the device can be fully controlled remotely.
    /// </summary>
    [Description("Whether the device can be fully controlled remotely.")]
    public bool fullRemoteControl { get; init; }

    /// <summary>
    /// Whether the device is in Smart Grid mode.
    /// </summary>
    [Description("Whether the device is in Smart Grid mode.")]
    public bool smartGrid { get; init; }

    /// <summary>
    /// Whether the device supports Mobile Start.
    /// </summary>
    [Description("Whether the device supports Mobile Start.")]
    public bool mobileStart { get; init; }
}
