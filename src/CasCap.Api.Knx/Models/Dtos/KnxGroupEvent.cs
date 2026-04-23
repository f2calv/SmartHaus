using Knx.Falcon;

namespace CasCap.Models.Dtos;

/// <summary>
/// This object exists because <see cref="Knx.Falcon.GroupEventArgs"/> is sealed and has no constructor to aid deserialization.
/// Conversion from <see cref="Knx.Falcon.GroupEventArgs"/> is handled by
/// <see cref="CasCap.Extensions.CemiLDataExtensions.ToKnxGroupEvent"/>.
/// </summary>
public record KnxGroupEvent
{
    /// <summary>
    /// Type of group event (e.g. <see cref="GroupEventType.ValueRead"/>, <see cref="GroupEventType.ValueWrite"/>, <see cref="GroupEventType.ValueResponse"/>).
    /// </summary>
    public GroupEventType EventType { get; init; }

    /// <summary>
    /// Priority of the KNX telegram (e.g. <see cref="Knx.Falcon.MessagePriority.System"/>, <see cref="Knx.Falcon.MessagePriority.Low"/>).
    /// </summary>
    public MessagePriority MessagePriority { get; init; }

    /// <summary>
    /// Number of hops the telegram has traversed through line/area couplers.
    /// </summary>
    public int HopCount { get; init; }

    /// <summary>
    /// Payload of the group telegram containing the datapoint value and encoding metadata.
    /// </summary>
    public KnxGroupValue Value { get; init; } = default!;

    /// <summary>
    /// Individual address of the device that sent the telegram.
    /// </summary>
    public KnxSourceAddress SourceAddress { get; init; } = default!;

    /// <summary>
    /// Group address targeted by the telegram.
    /// </summary>
    public KnxDestinationAddress DestinationAddress { get; init; } = default!;

    /// <summary>
    /// Indicates whether the telegram was transmitted using KNX Secure.
    /// </summary>
    public bool IsSecure { get; init; }

    /// <inheritdoc/>
    public override string ToString() => $"{SourceAddress.IndividualAddress} -> {DestinationAddress.GroupAddress}";
}
