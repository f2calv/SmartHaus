namespace CasCap.Models;

/// <summary>
/// Represents a single Shelly Plug S device within <see cref="ShellyConfig.Devices"/>.
/// </summary>
public record ShellyDevice
{
    /// <summary>
    /// The Shelly device ID to control and monitor.
    /// </summary>
    [Required, MinLength(1)]
    public required string DeviceId { get; init; }

    /// <summary>
    /// A human-readable name for this device (e.g. "Kitchen Plug", "Office Plug").
    /// </summary>
    [Required, MinLength(1)]
    public required string DeviceName { get; init; }

    /// <summary>The relay channel index to control.</summary>
    /// <remarks>Defaults to <c>0</c> (the only channel on a Shelly Plug S).</remarks>
    [Range(0, 3)]
    public int Channel { get; init; } = 0;
}
