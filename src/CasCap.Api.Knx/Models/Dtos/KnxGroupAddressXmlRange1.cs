namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a level-1 group range in the ETS XML export (e.g. Central, DG, OG, EG, KG).
/// </summary>
[Serializable]
public record KnxGroupAddressXmlRange1
{
    /// <summary>
    /// Display name of the group range (e.g. "DG", "OG", "EG").
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
    /// Child level-2 group ranges (e.g. Lighting, ShutterControl, Heating).
    /// </summary>
    [XmlElement("GroupRange")]
    public List<KnxGroupAddressXmlRange2> GroupRange { get; init; } = [];

    /// <inheritdoc/>
    public override string ToString() => $"{Name}, {RangeStart}->{RangeEnd}";
}
