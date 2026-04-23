namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a single KNX group address element in the ETS XML export.
/// </summary>
[Serializable]
public record KnxGroupAddressXml
{
    /// <summary>
    /// Display name of the group address (e.g. "DG-LI-Büro-SW").
    /// </summary>
    [XmlAttribute]
    public required string Name { get; init; }

    /// <summary>
    /// Three-level group address (e.g. "1/2/3").
    /// </summary>
    [XmlAttribute]
    public required string Address { get; init; }

    /// <summary>
    /// KNX datapoint type identifier (e.g. "DPST-1-1").
    /// </summary>
    [XmlAttribute]
    public string? DPTs { get; init; }

    /// <summary>
    /// Optional free-text description assigned in ETS.
    /// </summary>
    [XmlAttribute]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <inheritdoc />
    public override string ToString() => $"{Name}, {Address}, {DPTs}";
}
