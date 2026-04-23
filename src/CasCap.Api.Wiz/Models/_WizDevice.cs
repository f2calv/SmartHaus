namespace CasCap.Models;

/// <summary>
/// Represents a single known Wiz bulb within <see cref="WizConfig.Devices"/>.
/// Maps a MAC address to a human-readable name for identification and control.
/// </summary>
public record WizDevice
{
    /// <summary>
    /// MAC address of the Wiz bulb (e.g. "a8bb50aabbcc").
    /// Used to match discovered bulbs to their configured names.
    /// </summary>
    [Required, MinLength(1)]
    public required string Mac { get; init; }

    /// <summary>
    /// A human-readable name for this bulb (e.g. "Living Room Lamp", "Bedroom Ceiling").
    /// </summary>
    [Required, MinLength(1)]
    public required string DeviceName { get; init; }
}
