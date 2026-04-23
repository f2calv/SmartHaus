namespace CasCap.Models.Dtos;

/// <summary>
/// This object is a translation of the <see cref="Knx.Falcon.GroupEventArgs.SourceAddress"/> object.
/// </summary>
public record KnxSourceAddress
{
    /// <summary>
    /// String representation of the individual address (e.g. "1.1.1").
    /// </summary>
    /// <remarks>Custom</remarks>
    public required string IndividualAddress { get; init; }

    /// <summary>
    /// Resolved display name of the device, if available.
    /// </summary>
    public string? AddressName { get; init; }

    /// <summary>
    /// Area part of the individual address (high nibble, 0–15).
    /// </summary>
    public int AreaAddress { get; init; }

    /// <summary>
    /// Line part of the individual address (low nibble of the high byte, 0–15).
    /// </summary>
    public int LineAddress { get; init; }

    /// <summary>
    /// Device part of the individual address (low byte, 0–255).
    /// </summary>
    public int DeviceAddress { get; init; }

    /// <summary>
    /// Raw 16-bit representation of the individual address.
    /// </summary>
    public int FullAddress { get; init; }

    /// <summary>
    /// Combined area and line portion of the individual address (high byte).
    /// </summary>
    public int SubnetAddress { get; init; }

    /// <inheritdoc/>
    public override string ToString() => IndividualAddress;
}
