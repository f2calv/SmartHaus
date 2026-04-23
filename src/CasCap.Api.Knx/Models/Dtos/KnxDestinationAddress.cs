namespace CasCap.Models.Dtos;

/// <summary>
/// This object is a translation of the <see cref="Knx.Falcon.GroupEventArgs.DestinationAddress"/> object.
/// </summary>
public record KnxDestinationAddress
{
    /// <summary>
    /// String representation of the group address (e.g. "1/2/3").
    /// </summary>
    /// <remarks>Custom</remarks>
    public required string GroupAddress { get; init; }

    /// <summary>
    /// Raw 16-bit representation of the group address.
    /// </summary>
    public int FullAddress { get; init; }

    /// <inheritdoc/>
    public override string ToString() => GroupAddress;
}
