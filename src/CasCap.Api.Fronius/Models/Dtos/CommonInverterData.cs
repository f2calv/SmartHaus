namespace CasCap.Models.Dtos;

/// <summary>
/// Common inverter real-time data from <c>GetInverterRealtimeData.cgi?Datacollection=CommonInverterData</c>.
/// </summary>
public record CommonInverterData
{
    /// <summary>
    /// Energy generated today.
    /// </summary>
    [Description("Energy generated today.")]
    public UnitValue? DAY_ENERGY { get; init; }

    /// <summary>
    /// Current device status.
    /// </summary>
    [Description("Current device status.")]
    public DeviceStatus? DeviceStatus { get; init; }

    /// <summary>
    /// AC frequency.
    /// </summary>
    [Description("AC frequency.")]
    public UnitValue? FAC { get; init; }

    /// <summary>
    /// AC current.
    /// </summary>
    [Description("AC current.")]
    public UnitValue? IAC { get; init; }

    /// <summary>
    /// DC current (string 1).
    /// </summary>
    [Description("DC current (string 1).")]
    public UnitValue? IDC { get; init; }

    /// <summary>
    /// DC current (string 2).
    /// </summary>
    [Description("DC current (string 2).")]
    public UnitValue? IDC_2 { get; init; }

    /// <summary>
    /// DC current (string 3).
    /// </summary>
    [Description("DC current (string 3).")]
    public UnitValue? IDC_3 { get; init; }

    /// <summary>
    /// AC power output.
    /// </summary>
    [Description("AC power output.")]
    public UnitValue? PAC { get; init; }

    /// <summary>
    /// AC apparent power.
    /// </summary>
    [Description("AC apparent power.")]
    public UnitValue? SAC { get; init; }

    /// <summary>
    /// Total energy generated over the lifetime.
    /// </summary>
    [Description("Total energy generated over the lifetime.")]
    public UnitValue? TOTAL_ENERGY { get; init; }

    /// <summary>
    /// AC voltage.
    /// </summary>
    [Description("AC voltage.")]
    public UnitValue? UAC { get; init; }

    /// <summary>
    /// DC voltage (string 1).
    /// </summary>
    [Description("DC voltage (string 1).")]
    public UnitValue? UDC { get; init; }

    /// <summary>
    /// DC voltage (string 2).
    /// </summary>
    [Description("DC voltage (string 2).")]
    public UnitValue? UDC_2 { get; init; }

    /// <summary>
    /// DC voltage (string 3).
    /// </summary>
    [Description("DC voltage (string 3).")]
    public UnitValue? UDC_3 { get; init; }

    /// <summary>
    /// Energy generated this year.
    /// </summary>
    [Description("Energy generated this year.")]
    public UnitValue? YEAR_ENERGY { get; init; }
}
