namespace CasCap.Models;

/// <summary>
/// Represents an outbound telegram to be sent to the KNX bus. Replaces the
/// (<see cref="KnxGroupAddressParsed"/>, <see cref="Knx.Falcon.GroupValue"/>) tuple to enable serialization
/// for cross-pod transport via Redis streams.
/// </summary>
[MessagePackObject(true)]
public record KnxOutgoingTelegram
{
    /// <inheritdoc cref="KnxGroupAddressParsed" />
    [Required]
    public required KnxGroupAddressParsed Kga { get; init; }

    /// <summary>
    /// Raw byte payload of the <see cref="Knx.Falcon.GroupValue"/> to write to the bus.
    /// </summary>
    [Required]
    public required byte[] GroupValueData { get; init; }
}
