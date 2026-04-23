namespace CasCap.Models;

/// <summary>Represents a Shelly smart plug status reading event.</summary>
public record ShellyEvent
{
    /// <summary>Initializes a new instance from a Cloud API device status response.</summary>
    /// <param name="device">The device configuration that produced this event.</param>
    /// <param name="response">The device status response from the Shelly Cloud API.</param>
    public ShellyEvent(ShellyDevice device, ShellyDeviceStatusResponse response)
    {
        DeviceId = device.DeviceId;
        DeviceName = device.DeviceName;
        TimestampUtc = DateTime.UtcNow;
        var relay = response.Data?.DeviceStatus?.Relays?.FirstOrDefault();
        var meter = response.Data?.DeviceStatus?.Meters?.FirstOrDefault();
        RelayState = relay?.IsOn == true ? 1 : 0;
        Power = Math.Round(meter?.Power ?? 0, 1);
        Temperature = Math.Round(response.Data?.DeviceStatus?.Temperature ?? 0, 1);
        Overpower = relay?.Overpower == true ? 1 : 0;
    }

    internal ShellyEvent(string deviceId, string deviceName, double power, double relayState, double temperature, double overpower, DateTime timestampUtc)
    {
        DeviceId = deviceId;
        DeviceName = deviceName;
        Power = power;
        RelayState = relayState;
        Temperature = temperature;
        Overpower = overpower;
        TimestampUtc = timestampUtc;
    }

    /// <summary>The Shelly device ID that produced this reading.</summary>
    [Description("Shelly device ID")]
    public string DeviceId { get; init; }

    /// <summary>Human-readable name of the device.</summary>
    [Description("Human-readable device name")]
    public string DeviceName { get; init; }

    /// <summary>UTC timestamp of the reading.</summary>
    public DateTime TimestampUtc { get; init; }

    /// <summary>Instantaneous power consumption in Watts.</summary>
    [Description("Instantaneous power consumption of the connected load, units = Watts")]
    public double Power { get; init; }

    /// <summary>Relay state: 1 = on, 0 = off.</summary>
    [Description("Relay state — 1 = on, 0 = off")]
    public double RelayState { get; init; }

    /// <summary>Internal device temperature in degrees Celsius.</summary>
    [Description("Internal device temperature, units = Celsius")]
    public double Temperature { get; init; }

    /// <summary>Overpower condition: 1 = triggered, 0 = normal.</summary>
    [Description("Overpower condition — 1 = triggered, 0 = normal")]
    public double Overpower { get; init; }
}
