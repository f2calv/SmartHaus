namespace CasCap.Models;

/// <summary>
/// A point-in-time snapshot of Sicce water pump device state.
/// </summary>
public record SicceSnapshot
{
    /// <summary>Device temperature in degrees Celsius.</summary>
    [Description("Device temperature in degrees Celsius.")]
    public double Temperature { get; init; }

    /// <summary>Power level as a ratio (0.05–1.0).</summary>
    [Description("Pump power level as a ratio, range 0.05–1.0.")]
    public double Power { get; init; }

    /// <summary>Whether the device is online and reachable.</summary>
    [Description("Whether the device is currently online.")]
    public bool IsOnline { get; init; }

    /// <summary>Whether the pump power switch is on.</summary>
    [Description("Whether the pump power switch is currently on.")]
    public bool PowerSwitch { get; init; }

    /// <summary>UTC timestamp of the last reading.</summary>
    [Description("UTC timestamp of the snapshot.")]
    public DateTimeOffset? ReadingUtc { get; init; }
}
