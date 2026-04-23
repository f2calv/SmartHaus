namespace CasCap.Models.Dtos;

/// <summary>
/// This is the root object for the XML export of group addresses from ETS.
/// It contains a list of <see cref="KnxGroupAddressXmlRange1"/> objects,
/// which represent the group ranges and their associated group addresses.
/// The XML structure is defined by the KNX standard for group address export,
/// and this class is designed to facilitate the deserialization of that XML data into .NET objects.
/// </summary>
[Serializable, XmlRoot(Namespace = "http://knx.org/xml/ga-export/01", ElementName = "GroupAddress-Export")]
public record KnxGroupAddressXmlExport
{
    /// <summary>
    /// Top-level group ranges (e.g. Zentral, DG, OG, EG, KG, Data).
    /// </summary>
    [XmlElement("GroupRange")]
    public required List<KnxGroupAddressXmlRange1> GroupRange { get; init; }
}
