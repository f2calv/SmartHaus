namespace CasCap.Models;

/// <summary>Represents a Wiz smart bulb state event captured during a discovery cycle.</summary>
public record WizEvent
{
    /// <summary>Unique device identifier (typically the MAC address).</summary>
    [Description("Unique device identifier")]
    public required string DeviceId { get; init; }

    /// <summary>IP address of the bulb on the local network.</summary>
    [Description("Bulb IP address on the local network")]
    public required string IpAddress { get; init; }

    /// <summary>MAC address of the bulb.</summary>
    [Description("Bulb MAC address")]
    public string? Mac { get; init; }

    /// <summary>Human-readable device name, null if not configured.</summary>
    [Description("Friendly device name from configuration")]
    public string? DeviceName { get; init; }

    /// <summary>Whether the bulb is on or off.</summary>
    [Description("Whether the bulb is currently on")]
    public bool State { get; init; }

    /// <summary>Dimming level (10–100), null when off.</summary>
    [Description("Dimming level 10–100, null when off")]
    public int? Dimming { get; init; }

    /// <summary>Scene ID if a scene is active.</summary>
    [Description("Active scene ID, null if no scene")]
    public int? SceneId { get; init; }

    /// <summary>Colour temperature in Kelvin, null when using RGB.</summary>
    [Description("Colour temperature in Kelvin")]
    public int? Temp { get; init; }

    /// <summary>Red colour channel, 0–255.</summary>
    [Description("Red colour channel 0–255")]
    public int? R { get; init; }

    /// <summary>Green colour channel, 0–255.</summary>
    [Description("Green colour channel 0–255")]
    public int? G { get; init; }

    /// <summary>Blue colour channel, 0–255.</summary>
    [Description("Blue colour channel 0–255")]
    public int? B { get; init; }

    /// <summary>Cold white channel, 0–255.</summary>
    [Description("Cold white channel 0–255")]
    public int? C { get; init; }

    /// <summary>Warm white channel, 0–255.</summary>
    [Description("Warm white channel 0–255")]
    public int? W { get; init; }

    /// <summary>Wi-Fi signal strength in dBm.</summary>
    [Description("Wi-Fi RSSI in dBm")]
    public int? Rssi { get; init; }

    /// <summary>UTC timestamp of the event.</summary>
    public DateTime TimestampUtc { get; init; }
}
