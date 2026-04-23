namespace CasCap.Models.Dtos;

/// <summary>
/// A point-in-time snapshot of key Shelly smart plug values.
/// </summary>
public record ShellySnapshot
{
    /// <summary>The Shelly device ID.</summary>
    [Description("Shelly device ID")]
    public string DeviceId { get; init; } = default!;

    /// <summary>Human-readable name of the device.</summary>
    [Description("Human-readable device name")]
    public string DeviceName { get; init; } = default!;

    /// <summary>Instantaneous power consumption (W).</summary>
    [Description("Instantaneous power consumption in watts")]
    public double Power { get; init; }

    /// <summary>Whether the relay is on.</summary>
    [Description("Whether the relay is currently on")]
    public bool IsOn { get; init; }

    /// <summary>Internal device temperature (°C).</summary>
    [Description("Internal device temperature in Celsius")]
    public double Temperature { get; init; }

    /// <summary>Whether an overpower condition is active.</summary>
    [Description("Whether an overpower condition is active")]
    public bool Overpower { get; init; }

    /// <summary>UTC timestamp of the last reading.</summary>
    [Description("UTC timestamp of the last reading")]
    public DateTimeOffset? ReadingUtc { get; init; }
}
