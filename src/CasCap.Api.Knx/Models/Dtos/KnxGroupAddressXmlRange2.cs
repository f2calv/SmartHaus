namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a level-2 group range in the ETS XML export (e.g. Lighting, ShutterControl, Heating).
/// </summary>
[Serializable]
public record KnxGroupAddressXmlRange2
{
    /// <summary>
    /// Display name of the group range (e.g. "Lighting", "Heating").
    /// </summary>
    [XmlAttribute]
    public required string Name { get; init; }

    /// <summary>
    /// Inclusive start of the address range.
    /// </summary>
    [XmlAttribute]
    public required int RangeStart { get; init; }

    /// <summary>
    /// Inclusive end of the address range.
    /// </summary>
    [XmlAttribute]
    public required int RangeEnd { get; init; }

    /// <summary>
    /// Child group addresses within this range.
    /// </summary>
    [XmlElement("GroupAddress")]
    public required List<KnxGroupAddressXml> GroupAddress { get; init; }

    /// <inheritdoc />
    public override string ToString() => $"{Name}, {RangeStart}->{RangeEnd}";
}
