namespace CasCap.Models;

/// <summary>A point-in-time snapshot of a Wiz smart bulb state.</summary>
public record WizSnapshot
{
    /// <summary>Unique device identifier.</summary>
    [Description("Unique device identifier.")]
    public string? DeviceId { get; init; }

    /// <summary>IP address of the bulb.</summary>
    [Description("Bulb IP address on the local network.")]
    public string? IpAddress { get; init; }

    /// <summary>MAC address of the bulb.</summary>
    [Description("Bulb MAC address.")]
    public string? Mac { get; init; }

    /// <summary>Whether the bulb is on or off.</summary>
    [Description("Whether the bulb is currently on.")]
    public bool State { get; init; }

    /// <summary>Dimming level (10–100).</summary>
    [Description("Dimming level 10–100.")]
    public int? Dimming { get; init; }

    /// <summary>Scene ID if a scene is active.</summary>
    [Description("Active scene ID.")]
    public int? SceneId { get; init; }

    /// <summary>Colour temperature in Kelvin.</summary>
    [Description("Colour temperature in Kelvin.")]
    public int? Temp { get; init; }

    /// <summary>Wi-Fi signal strength in dBm.</summary>
    [Description("Wi-Fi RSSI in dBm.")]
    public int? Rssi { get; init; }

    /// <summary>UTC timestamp of the last reading.</summary>
    [Description("UTC timestamp of the snapshot.")]
    public DateTimeOffset? ReadingUtc { get; init; }
}
