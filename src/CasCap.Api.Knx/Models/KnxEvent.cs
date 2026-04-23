using Knx.Falcon;

namespace CasCap.Models;

/// <summary>
/// This object is to wrap the <see cref="KnxGroupEvent"/> object along with timestamp,
/// a <see cref="KnxGroupAddressParsed"/> object and the actual value.
/// </summary>
public record KnxEvent
{
    /// <summary>Initialises a new instance of the <see cref="KnxEvent"/> record.</summary>
    public KnxEvent(DateTime timestampUtc, KnxGroupEvent args, KnxGroupAddressParsed kga, GroupValue groupValue,
        object value, string? valueLabel)
    {
        TimestampUtc = timestampUtc;
        Args = args;
        Kga = kga;
        GroupValue = groupValue;
        Value = value;
        ValueLabel = valueLabel;
    }

    /// <summary>
    /// UTC timestamp of when the telegram was received.
    /// </summary>
    public DateTime TimestampUtc { get; init; }

    /// <inheritdoc cref="KnxGroupEvent"/>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public KnxGroupEvent Args { get; init; }

    /// <inheritdoc cref="KnxGroupAddressParsed" />
    public KnxGroupAddressParsed Kga { get; init; }

    /// <inheritdoc cref="Knx.Falcon.GroupValue" />
    /// <remarks>
    /// Excluded from JSON serialization because <see cref="Knx.Falcon.GroupValue"/> is an external
    /// type without serialization attributes. The decoded value is available via <see cref="Value"/>
    /// and <see cref="ValueAsString"/>. This property is populated in-process by
    /// <see cref="CasCap.Services.KnxMonitorBgService"/> and is not needed after decoding.
    /// </remarks>
    [JsonIgnore]
    public GroupValue GroupValue { get; init; }

    /// <summary>The decoded bus value (e.g. <see langword="true"/>, <c>21.5</c>, <c>85</c>).</summary>
    public object Value { get; init; }

    /// <summary>
    /// Value e.g. False
    /// </summary>
    public string ValueAsString => Value.ToString()!.Trim();

    /// <summary>
    /// The human-readable label for the decoded bus value (e.g. Open/Closed, Active/Inaction, etc...).
    /// Sourced from the <see href="https://github.com/toolsfactory/Tiveria.Home.Knx">Tiveria</see> library.
    /// </summary>
    public string? ValueLabel { get; init; }

    /// <inheritdoc/>
    public override string ToString()
    {
        var valueLabel = string.IsNullOrWhiteSpace(ValueLabel) ? string.Empty : $" ({ValueLabel})";
        return $"'{Args.SourceAddress.IndividualAddress}' to '{Kga.GroupAddress}' ({Kga.Name}, '{Value}{valueLabel}')";
    }
}
