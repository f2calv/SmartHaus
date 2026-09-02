namespace CasCap.Models.Dtos;

/// <summary>
/// Power flow real-time data from <c>GetPowerFlowRealtimeData.fcgi</c>.
/// </summary>
public sealed record PowerFlowRealtimeData
{
    /// <summary>
    /// Dictionary of inverters keyed by device ID (e.g. "1").
    /// </summary>
    [Description("Dictionary of inverters keyed by device ID (e.g. \"1\").")]
    public Dictionary<string, PowerFlowInverter>? Inverters { get; init; }

    /// <summary>
    /// Secondary meters keyed by device ID.
    /// </summary>
    /// <remarks>The device-specific payload is preserved because its shape depends on the connected meter model.</remarks>
    [Description("Secondary meters keyed by device ID.")]
    public Dictionary<string, JsonElement>? SecondaryMeters { get; init; }

    /// <summary>
    /// Site-level power flow summary.
    /// </summary>
    [Description("Site-level power flow summary.")]
    public PowerFlowSite? Site { get; init; }

    /// <summary>
    /// Smart-load devices grouped by device family.
    /// </summary>
    [Description("Smart-load devices grouped by device family.")]
    public Smartloads? Smartloads { get; init; }

    /// <summary>
    /// The API version string.
    /// </summary>
    [Description("The API version string.")]
    public string? Version { get; init; }
}
