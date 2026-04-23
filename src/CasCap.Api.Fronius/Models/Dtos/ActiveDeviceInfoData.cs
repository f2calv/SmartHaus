namespace CasCap.Models.Dtos;

/// <summary>
/// Active device info from <c>GetActiveDeviceInfo.cgi</c>.
/// </summary>
public record ActiveDeviceInfoData
{
    /// <summary>
    /// Connected inverters keyed by device index.
    /// </summary>
    [Description("Connected inverters keyed by device index.")]
    public Dictionary<string, ActiveDeviceEntry>? Inverter { get; init; }

    /// <summary>
    /// Connected meters keyed by device index.
    /// </summary>
    [Description("Connected meters keyed by device index.")]
    public Dictionary<string, ActiveDeviceEntry>? Meter { get; init; }

    /// <summary>
    /// Connected storage devices keyed by device index.
    /// </summary>
    [Description("Connected storage devices keyed by device index.")]
    public Dictionary<string, ActiveDeviceEntry>? Storage { get; init; }

    /// <summary>
    /// Connected Ohmpilot devices keyed by device index.
    /// </summary>
    [Description("Connected Ohmpilot devices keyed by device index.")]
    public Dictionary<string, ActiveDeviceEntry>? Ohmpilot { get; init; }
}
