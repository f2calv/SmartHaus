namespace CasCap.Models.Dtos;

/// <summary>
/// Per-inverter power flow data returned inside <see cref="PowerFlowRealtimeData"/>.
/// </summary>
public record PowerFlowInverter
{
    /// <summary>
    /// Battery operating mode (e.g. "normal").
    /// </summary>
    [Description("Battery operating mode (e.g. \"normal\").")]
    public string? Battery_Mode { get; init; }

    /// <summary>
    /// Device type identifier.
    /// </summary>
    [Description("Device type identifier.")]
    public int DT { get; init; }

    /// <summary>
    /// Energy generated today in watt-hours.
    /// </summary>
    [Description("Energy generated today in watt-hours.")]
    public double? E_Day { get; init; }

    /// <summary>
    /// Total energy generated over the lifetime of the device in watt-hours.
    /// </summary>
    [Description("Total energy generated over the lifetime of the device in watt-hours.")]
    public double? E_Total { get; init; }

    /// <summary>
    /// Energy generated this year in watt-hours.
    /// </summary>
    [Description("Energy generated this year in watt-hours.")]
    public double? E_Year { get; init; }

    /// <summary>
    /// Current power output in watts.
    /// </summary>
    [Description("Current power output in watts.")]
    public double P { get; init; }

    /// <summary>
    /// Battery state of charge as a percentage (0–100).
    /// </summary>
    [Description("Battery state of charge as a percentage (0–100).")]
    public double SOC { get; init; }
}
