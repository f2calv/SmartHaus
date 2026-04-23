namespace CasCap.Models.Dtos;

/// <summary>
/// This object is a translation of the <see cref="Knx.Falcon.GroupEventArgs.Value"/> object.
/// </summary>
public record KnxGroupValue
{
    /// <summary>
    /// Decoded CLR value of the datapoint (e.g. <see langword="bool"/>, <see langword="int"/>, <see langword="float"/>).
    /// </summary>
    public required object TypedValue { get; init; }

    /// <summary>
    /// Raw byte payload of the group value as transmitted on the KNX bus.
    /// </summary>
    public required byte[] Value { get; init; }

    /// <summary>
    /// Size of the datapoint value in bits.
    /// </summary>
    public required int SizeInBit { get; init; }

    /// <summary>
    /// Indicates whether the value fits in the first 6 bits of the APCI byte (short group value).
    /// </summary>
    public required bool IsShort { get; init; }

    //public string type { get; init; }
}
