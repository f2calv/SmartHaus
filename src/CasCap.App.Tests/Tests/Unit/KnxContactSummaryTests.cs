using CasCap.Models.Dtos;

namespace CasCap.Tests.Unit;

/// <summary>Regression tests for physical KNX contact summaries.</summary>
[Trait("Category", "Knx")]
public sealed class KnxContactSummaryTests
{
    // Regression: the model reported aggregate/shutter states as partially open windows.
    [Fact]
    public void Create_CountsOnlyBinaryContactStates()
    {
        KnxContact[] contacts =
        [
            CreateContact("DG-BI-Office(Window)-North-R", DptState.Active),
            CreateContact("EG-BI-Entrance(FrontDoor)-East", DptState.Inactive),
            CreateContact("KG-BI-Study(Window)-West-L", null),
        ];

        var result = KnxContactSummary.Create(contacts);

        Assert.Equal(3, result.ContactCount);
        Assert.Equal(1, result.OpenCount);
        Assert.Equal(1, result.ClosedCount);
        Assert.Equal(1, result.UnknownCount);
    }

    [Theory]
    [InlineData("True", "open", DptState.Active)]
    [InlineData("False", "closed", DptState.Inactive)]
    [InlineData("True", null, DptState.Active)]
    [InlineData("False", null, DptState.Inactive)]
    public void FromGroup_DecodesLiveContactState(string value, string? valueLabel, DptState expected)
    {
        var group = new KnxGroupAddressGroup
        {
            GroupName = "DG-BI-Office(Window)-North-R",
            Category = GroupAddressCategory.BI,
            Children =
            [
                new KnxGroupAddressGroupFunction
                {
                    Name = "DG-BI-Office(Window)-North-R-STATE",
                    GroupAddress = "1/3/2",
                    Function = ContactFunction.STATE.ToString(),
                    DPTs = "DPST-1-19",
                    Value = value,
                    ValueLabel = valueLabel,
                },
            ],
        };

        var result = KnxContact.FromGroup(group);

        Assert.Equal(expected, result.State);
    }

    [Theory]
    [InlineData("DG-BI", GroupAddressCategory.BI, FloorType.DG, null, null, null, false)]
    [InlineData("EG-BI-LivingRoom", GroupAddressCategory.BI, FloorType.EG, RoomType.LivingRoom, null, null, false)]
    [InlineData("DG-BI-Office(Window)-North-R", GroupAddressCategory.BI, FloorType.DG, RoomType.Office, "Window", CompassOrientation.North, true)]
    [InlineData("DG-BL-Office-North-R", GroupAddressCategory.BL, FloorType.DG, RoomType.Office, null, CompassOrientation.North, false)]
    public void IsPhysicalContactGroup_ExcludesAggregatesAndShutters(
        string groupName,
        GroupAddressCategory category,
        FloorType floor,
        RoomType? room,
        string? location,
        CompassOrientation? orientation,
        bool expected)
    {
        var group = new KnxGroupAddressGroup
        {
            GroupName = groupName,
            Category = category,
            Floor = floor,
            Room = room,
            Location = location,
            Orientation = orientation,
        };

        Assert.Equal(expected, KnxQueryService.IsPhysicalContactGroup(group));
    }

    private static KnxContact CreateContact(string groupName, DptState? state)
        => new()
        {
            GroupName = groupName,
            State = state,
        };
}
