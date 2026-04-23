namespace CasCap.Models.Dtos;

/// <summary>
/// Power flow real-time data from <c>GetPowerFlowRealtimeData.fcgi</c>.
/// </summary>
public record PowerFlowRealtimeData
{
    /// <summary>
    /// Dictionary of inverters keyed by device ID (e.g. "1").
    /// </summary>
    [Description("Dictionary of inverters keyed by device ID (e.g. \"1\").")]
    public Dictionary<string, PowerFlowInverter>? Inverters { get; init; }

    /// <summary>
    /// Site-level power flow summary.
    /// </summary>
    [Description("Site-level power flow summary.")]
    public PowerFlowSite? Site { get; init; }

    /// <summary>
    /// The API version string.
    /// </summary>
    [Description("The API version string.")]
    public string? Version { get; init; }
}
