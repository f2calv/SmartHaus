namespace CasCap.Models.Dtos;

/// <summary>Represents a discovered Wiz bulb on the local network.</summary>
public record WizBulb
{
    /// <summary>IP address of the bulb on the local network.</summary>
    [Description("IP address of the bulb on the local network.")]
    public required string IpAddress { get; init; }

    /// <summary>MAC address of the bulb.</summary>
    [Description("MAC address of the bulb.")]
    public string? Mac { get; init; }

    /// <summary>Human-readable name from <see cref="WizConfig.Devices"/>, or null if not configured.</summary>
    [Description("Friendly device name, null if not mapped in configuration.")]
    public string? DeviceName { get; init; }

    /// <summary>Current pilot state of the bulb.</summary>
    [Description("Current pilot state of the bulb — on/off, brightness, colour, temperature.")]
    public WizPilotState? PilotState { get; init; }

    /// <summary>System configuration of the bulb.</summary>
    [Description("System configuration — firmware version, module name, home and room IDs.")]
    public WizSystemConfig? SystemConfig { get; init; }

    /// <summary>UTC timestamp when the bulb was last seen during discovery.</summary>
    [Description("UTC timestamp when the bulb was last seen during discovery.")]
    public DateTime LastSeen { get; init; }
}
