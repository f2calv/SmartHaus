namespace CasCap.Models.Dtos;

/// <summary>
/// Identification information for a Miele device.
/// </summary>
public record MieleIdent
{
    /// <summary>
    /// The device type (e.g. Oven, Dishwasher, Washing machine).
    /// </summary>
    [Description("The device type (e.g. Oven, Dishwasher, Washing machine).")]
    public LocalizedValue? type { get; init; }

    /// <summary>
    /// The user-defined friendly name of the device.
    /// </summary>
    [Description("The user-defined friendly name of the device.")]
    public string? deviceName { get; init; }

    /// <summary>
    /// The supported protocol version.
    /// </summary>
    [Description("The supported protocol version.")]
    public int? protocolVersion { get; init; }

    /// <summary>
    /// Hardware identification label.
    /// </summary>
    [Description("Hardware identification label.")]
    public DeviceIdentLabel? deviceIdentLabel { get; init; }

    /// <summary>
    /// Communication module identification label.
    /// </summary>
    [Description("Communication module identification label.")]
    public XkmIdentLabel? xkmIdentLabel { get; init; }
}
