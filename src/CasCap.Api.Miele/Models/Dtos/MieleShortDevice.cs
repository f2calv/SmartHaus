namespace CasCap.Models.Dtos;

/// <summary>
/// Shortened device information from <c>GET /short/devices</c>.
/// </summary>
public record MieleShortDevice
{
    /// <summary>
    /// The serial number of the device.
    /// </summary>
    [Description("The serial number of the device.")]
    public string? fabNumber { get; init; }

    /// <summary>
    /// The localized device state.
    /// </summary>
    [Description("The localized device state.")]
    public string? state { get; init; }

    /// <summary>
    /// The localized device type.
    /// </summary>
    [Description("The localized device type.")]
    public string? type { get; init; }

    /// <summary>
    /// The friendly name of the device.
    /// </summary>
    [Description("The friendly name of the device.")]
    public string? deviceName { get; init; }

    /// <summary>
    /// The URL to the full device information.
    /// </summary>
    [Description("The URL to the full device information.")]
    public string? details { get; init; }
}
