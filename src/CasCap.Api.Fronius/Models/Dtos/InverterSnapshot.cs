namespace CasCap.Models.Dtos;

/// <summary>
/// A point-in-time snapshot of key Fronius solar inverter values.
/// </summary>
public sealed record InverterSnapshot
{
    /// <summary>
    /// Battery state of charge as a percentage from 0 to 100.
    /// </summary>
    /// <remarks>The sinks store a 0–1 ratio; it is converted here so a model never reads 0.07 as 0.07 %.</remarks>
    [Description("Battery state of charge as a percentage from 0 to 100, for example 6.7 means 6.7 %")]
    public double StateOfChargePercent { get; init; }

    /// <summary>
    /// Battery power (W). Positive = discharging, negative = charging.
    /// </summary>
    [Description("Battery power in watts: positive means the battery is discharging, negative means it is charging")]
    public double BatteryPower { get; init; }

    /// <summary>
    /// Grid power (W). Positive = importing from the grid, negative = exporting to the grid.
    /// </summary>
    [Description("Grid power in watts: positive means importing from the grid, negative means exporting to the grid")]
    public double GridPower { get; init; }

    /// <summary>
    /// Household load power (W), reported negative by the inverter.
    /// </summary>
    [Description("Household consumption in watts, reported as a negative number; -350 means the house is using 350 W")]
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

    /// <summary>
    /// Seconds between <see cref="ReadingUtc"/> and the time of the query.
    /// </summary>
    [Description("Age of the reading in seconds when it was queried; readings are normally a few seconds old")]
    public double? ReadingAgeSeconds { get; init; }
}
