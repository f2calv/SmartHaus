using CasCap.Models.Dtos;

namespace CasCap.Tests.Unit;

/// <summary>Regression tests for KNX Boolean door-lock summaries.</summary>
[Trait("Category", "Knx")]
public sealed class KnxDoorLockSummaryTests
{
    [Theory]
    [InlineData("False", "open", DptLockState.Locked)]
    [InlineData("0", null, DptLockState.Locked)]
    [InlineData("True", "closed", DptLockState.Unlocked)]
    [InlineData("1", null, DptLockState.Unlocked)]
    public void FromGroup_DecodesConfirmedPolarityFromRawValue(
        string value,
        string? staleValueLabel,
        DptLockState expected)
    {
        var result = KnxDoorLock.FromGroup(CreateLockGroup(value, staleValueLabel));

        Assert.Equal(expected, result.State);
    }

    [Fact]
    public void Create_CountsLockStates()
    {
        KnxDoorLock[] locks =
        [
            CreateLock("EG-BI-Entrance(Main DoorLock)-North", DptLockState.Locked),
            CreateLock("KG-BI-GuestRoom(Patio DoorLock)-South", DptLockState.Unlocked),
            CreateLock("EG-BI-Entrance(Unknown DoorLock)-East", null),
        ];

        var result = KnxDoorLockSummary.Create(locks);

        Assert.Equal(3, result.LockCount);
        Assert.Equal(1, result.LockedCount);
        Assert.Equal(1, result.UnlockedCount);
        Assert.Equal(1, result.UnknownCount);
    }

    [Fact]
    public void IsDoorLockGroup_RequiresNameAndBooleanDpt()
    {
        var lockGroup = CreateLockGroup("False", null);
        var openingContact = lockGroup with { Location = "Main Door" };
        var wrongDpt = lockGroup with
        {
            Children =
            [
                lockGroup.Children[0] with { DPTs = "DPST-1-19" },
            ],
        };

        Assert.True(KnxQueryService.IsDoorLockGroup(lockGroup));
        Assert.False(KnxQueryService.IsPhysicalContactGroup(lockGroup));
        Assert.False(KnxQueryService.IsDoorLockGroup(openingContact));
        Assert.False(KnxQueryService.IsDoorLockGroup(wrongDpt));
    }

    private static KnxDoorLock CreateLock(string groupName, DptLockState? state)
        => new()
        {
            GroupName = groupName,
            Location = "DoorLock",
            State = state,
        };

    private static KnxGroupAddressGroup CreateLockGroup(string value, string? valueLabel)
        => new()
        {
            GroupName = "EG-BI-Entrance(Main DoorLock)-North",
            Category = GroupAddressCategory.BI,
            Floor = FloorType.EG,
            Room = RoomType.Entrance,
            Location = "Main DoorLock",
            Orientation = CompassOrientation.North,
            Children =
            [
                new KnxGroupAddressGroupFunction
                {
                    Name = "EG-BI-Entrance(Main DoorLock)-North-STATE",
                    GroupAddress = "3/3/1",
                    Function = ContactFunction.STATE.ToString(),
                    DPTs = "DPST-1-2",
                    Major = 1,
                    Minor = 2,
                    Value = value,
                    ValueLabel = valueLabel,
                },
            ],
        };
}