namespace CasCap.Models;

/// <summary>Represents a Sicce water pump device reading event.</summary>
public record SicceEvent
{
    /// <summary>Initializes a new instance from a <see cref="DeviceInfo"/> API response.</summary>
    /// <param name="deviceInfo">Device information from the Sicce cloud API.</param>
    public SicceEvent(DeviceInfo deviceInfo)
    {
        TimestampUtc = DateTime.UtcNow;
        Temperature = deviceInfo.Temperature;
        Power = deviceInfo.Power / 100.0;
        IsOnline = deviceInfo.IsOnline;
        PowerSwitch = deviceInfo.PowerSwitch;
    }

    internal SicceEvent(double temperature, double power, bool isOnline, bool powerSwitch, DateTime timestampUtc)
    {
        Temperature = temperature;
        Power = power;
        IsOnline = isOnline;
        PowerSwitch = powerSwitch;
        TimestampUtc = timestampUtc;
    }

    /// <summary>UTC timestamp of the reading.</summary>
    public DateTime TimestampUtc { get; init; }

    /// <summary>Device temperature in degrees Celsius.</summary>
    [Description("Device temperature in degrees Celsius")]
    public double Temperature { get; init; }

    /// <summary>Power level as a ratio (0.05–1.0).</summary>
    [Description("Pump power level as a ratio, range 0.05–1.0")]
    public double Power { get; init; }

    /// <summary>Whether the device is online and reachable.</summary>
    [Description("Whether the device is currently online")]
    public bool IsOnline { get; init; }

    /// <summary>Whether the pump power switch is on.</summary>
    [Description("Whether the pump power switch is currently on")]
    public bool PowerSwitch { get; init; }
}
