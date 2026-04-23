namespace CasCap.Models.Dtos;

/// <summary>
/// A point-in-time snapshot of key Fronius solar inverter values.
/// </summary>
public record InverterSnapshot
{
    /// <summary>
    /// Battery state of charge (%).
    /// </summary>
    [Description("Battery state of charge percentage")]
    public double StateOfCharge { get; init; }

    /// <summary>
    /// Battery power (W). Positive = charging, negative = discharging.
    /// </summary>
    [Description("Battery power in watts")]
    public double BatteryPower { get; init; }

    /// <summary>
    /// Grid power (W). Positive = consuming from grid, negative = feeding to grid.
    /// </summary>
    [Description("Grid power in watts")]
    public double GridPower { get; init; }

    /// <summary>
    /// Household load power (W).
    /// </summary>
    [Description("Household load power in watts")]
    public double LoadPower { get; init; }

    /// <summary>
    /// Photovoltaic production power (W).
    /// </summary>
    [Description("Photovoltaic production power in watts")]
    public double PhotovoltaicPower { get; init; }

    /// <summary>
    /// UTC timestamp of the last reading.
    /// </summary>
    [Description("UTC timestamp of the last reading")]
    public DateTimeOffset? ReadingUtc { get; init; }
}
