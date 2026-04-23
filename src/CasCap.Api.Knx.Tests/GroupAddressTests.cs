namespace CasCap.Tests;

/// <summary>
/// Integration tests for KNX group address parsing and lookup.
/// </summary>
public class GroupAddressTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public void UnixTimeMS()
    {
        var dt = DateTime.UtcNow;

        var unixMS = dt.ToUnixTimeMs();

        var utcNow = unixMS.FromUnixTimeMs();
        //another Equals() issue...
        Assert.True(dt.ToString() == utcNow.ToString());
    }

    [Fact]
    public void GetInnerText()
    {
        var inner = "Wibble";
        var sections = new[] { $"[{inner}]" };
        var o = KnxGroupAddressParsed.GetInnerText(ref sections, "[", "]");
        Assert.True(inner == o);
        Assert.Empty(sections);
    }

    [Fact]
    public async Task ParseGroupAddressNamingConvention()
    {
        var address_errors = new List<KnxGroupAddressParsed>();
        var i = 0;
        var gaLookup = await _knxGroupAddressLookupSvc.GetLookup();
        foreach (var kvp in gaLookup)
        {
            var kga = kvp.Value;
            //if (kga.Name != "DG-BI-STATE")
            //    continue;
            if (kga.Category != GroupAddressCategory.Unknown)
                if (kga.sections.Length != 0)
                {
                    address_errors.Add(kvp.Value);
                    _output.WriteLine(string.Join(',', kga.sections) + $" ({kga.Name}, {kga.GroupAddress})");
                }
                else
                    i++;
        }
        Assert.Empty(address_errors);
        Assert.True(i > 0);
        _output.WriteLine($"'{i}' group address names parsed successfully");
    }

    [Theory(Skip = "unfinished")]
    //[InlineData(4352, "2/1/0")]
    //[InlineData(512, "0/2/0")]
    [InlineData(1536, "0/6/0")]
    public void ParseGroupAddressInt(int Address, string AddressString)
    {
        var val = Address;
        //var ranges = val % 255;
        //ranges: main = 0..31, middle = 0..7, sub = 0..255
        //area/line/?
        for (var main = 0; main <= 31; main++)
        {
            for (var middle = 0; middle <= 7; middle++)
            {
                if (val <= 254)
                {
                    var str = $"{main}/{middle}/{val}";
                    Debug.WriteLine(str);
                    Assert.Equal(str, AddressString);
                    break;
                }
                else
                    val -= 254;
            }
        }
    }

    [Fact]
    public async Task DeserializedGroupAddressCountMatchesXml()
    {
        var path = _serviceProvider.GetRequiredService<IOptions<KnxConfig>>().Value.GroupAddressXmlFilePath;

        var deserialized = await KnxGroupAddressLookupService.DeserializeGroupAddressesFromFile(path);

        //count GroupAddress elements directly from the raw XML
        var xml = await File.ReadAllTextAsync(path);
        var xDoc = XDocument.Parse(xml);
        XNamespace ns = "http://knx.org/xml/ga-export/01";
        var xmlElementCount = xDoc.Descendants(ns + "GroupAddress").Count();

        _output.WriteLine($"Deserialized: {deserialized.Count}, XML elements: {xmlElementCount}");
        Assert.Equal(xmlElementCount, deserialized.Count);
    }

    [Fact]
    public async Task GroupedAddressesContainAllChildren()
    {
        var gaLookup = await _knxGroupAddressLookupSvc.GetLookup();
        var groups = await _knxGroupAddressLookupSvc.GetGroupAddressesGrouped();

        var totalChildren = groups.Sum(g => g.Children.Count);
        _output.WriteLine($"Groups: {groups.Count}, Children: {totalChildren}, Lookup: {gaLookup.Count}");

        Assert.Equal(gaLookup.Count, totalChildren);
        Assert.True(groups.Count > 0);
        Assert.True(groups.Count < gaLookup.Count);

        foreach (var group in groups)
        {
            Assert.NotNull(group.GroupName);
            Assert.NotEqual(GroupAddressCategory.Unknown, group.Category);
            Assert.True(group.Children.Count > 0);

            foreach (var child in group.Children)
            {
                Assert.True(child.Name.StartsWith(group.GroupName),
                    $"Child '{child.Name}' does not start with group '{group.GroupName}'");
            }
        }
    }
}
