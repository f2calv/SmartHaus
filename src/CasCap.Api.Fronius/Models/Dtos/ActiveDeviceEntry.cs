namespace CasCap.Models.Dtos;

/// <summary>
/// A single active device entry containing its type and serial number.
/// </summary>
public record ActiveDeviceEntry
{
    /// <summary>
    /// Device type identifier.
    /// </summary>
    [Description("Device type identifier.")]
    public int DT { get; init; }

    /// <summary>
    /// Device serial number.
    /// </summary>
    [Description("Device serial number.")]
    public string? Serial { get; init; }
}
