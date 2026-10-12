using CasCap.Models.Dtos;

namespace CasCap.Tests;

/// <summary>Verifies shutter progress tracking and deferred slat commands.</summary>
public sealed class KnxShutterMovementTests
{
    private const string Kitchen = "EG-BL-Kitchen-South";

    [Fact]
    public async Task AdvanceAsync_SendsSlatsOnceAfterPositionIsReached()
    {
        var states = new Queue<KnxShutter>(
        [
            new() { GroupName = Kitchen, Position = 40, SlatsPosition = 50 },
            new() { GroupName = Kitchen, Position = 98, SlatsPosition = 50 },
            new() { GroupName = Kitchen, Position = 98, SlatsPosition = 50 },
            new() { GroupName = Kitchen, Position = 98, SlatsPosition = 100 },
        ]);
        var commands = new List<KnxShutterStateChangeRequest>();
        var movement = new KnxShutterMovement(
            [new KnxShutterTarget { GroupName = Kitchen, Position = 100, Slats = 100 }],
            5,
            _ => Task.FromResult(new List<KnxShutter> { states.Dequeue() }),
            (request, _) =>
            {
                commands.Add(request);
                return Task.CompletedTask;
            });
        var token = TestContext.Current.CancellationToken;

        Assert.Equal(0, (await movement.AdvanceAsync(token)).Completed);
        Assert.Empty(commands);
        Assert.Equal(0, (await movement.AdvanceAsync(token)).Completed);
        Assert.Equal(0, (await movement.AdvanceAsync(token)).Completed);
        var final = await movement.AdvanceAsync(token);

        var command = Assert.Single(commands);
        Assert.Equal(100, command.Slats);
        Assert.Null(command.VPosition);
        Assert.Equal(1, final.Completed);
        Assert.Empty(final.Pending);
    }

    [Fact]
    public async Task AdvanceAsync_UnknownShutterRemainsPending()
    {
        var movement = new KnxShutterMovement(
            [new KnxShutterTarget { GroupName = Kitchen, Position = 0 }],
            5,
            _ => Task.FromResult(new List<KnxShutter>()),
            (_, _) => Task.CompletedTask);

        var (completed, pending) = await movement.AdvanceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, completed);
        Assert.Equal([Kitchen], pending);
    }

    [Fact]
    public async Task AdvanceAsync_SlatsWithoutFeedbackCompleteOnceCommanded()
    {
        var commands = new List<KnxShutterStateChangeRequest>();
        var movement = new KnxShutterMovement(
            [new KnxShutterTarget { GroupName = Kitchen, Position = 100, Slats = 100 }],
            5,
            _ => Task.FromResult(new List<KnxShutter> { new() { GroupName = Kitchen, Position = 100 } }),
            (request, _) =>
            {
                commands.Add(request);
                return Task.CompletedTask;
            });
        var token = TestContext.Current.CancellationToken;

        Assert.Equal(0, (await movement.AdvanceAsync(token)).Completed);
        Assert.Equal(1, (await movement.AdvanceAsync(token)).Completed);
        Assert.Single(commands);
    }

    [Fact]
    public void IndividualShutterGroups_ExcludesIgnoredAndPositionlessGroups()
    {
        KnxGroupAddressGroup[] groups =
        [
            Blind("OG-BL-MasterBedroom", "POS", "POS_FB"),
            Blind("OG-BL-MasterBedroom-South", "POS", "POS_FB"),
            Blind("OG-BL-ChildRoom", "MOVE", "STEP"),
        ];

        var visible = KnxGroupAddressLookupService.ExcludeIgnored([.. groups], ["OG-BL-MasterBedroom"]);
        var names = KnxQueryService.IndividualShutterGroups(visible).Select(g => g.GroupName);

        Assert.Equal(["OG-BL-MasterBedroom-South"], names);
    }

    private static KnxGroupAddressGroup Blind(string groupName, params string[] functions)
        => new()
        {
            GroupName = groupName,
            Category = GroupAddressCategory.BL,
            Floor = FloorType.OG,
            Room = RoomType.MasterBedroom,
            Children = [.. functions.Select(f => new KnxGroupAddressGroupFunction
            {
                Name = $"{groupName}-{f}",
                GroupAddress = "1/2/3",
                Function = f,
                DPTs = "DPST-5-1",
            })],
        };

    [Theory]
    [InlineData(98d, 100d, true)]
    [InlineData(94d, 100d, false)]
    [InlineData(null, 0d, false)]
    public void IsAt_AppliesTolerance(double? actual, double desired, bool expected)
        => Assert.Equal(expected, KnxShutterMovement.IsAt(actual, desired, 5));

    [Theory]
    [InlineData("True", "Down", true)]
    [InlineData("True", null, true)]
    [InlineData("1", null, true)]
    [InlineData("False", "Up", false)]
    [InlineData("0", null, false)]
    public void IsDirectionDown_ReadsDpt1008(string value, string? label, bool expected)
        => Assert.Equal(expected, KnxQueryService.IsDirectionDown(new State("EG-BL-Kitchen-South-DIRECTION", value, label, DateTime.UnixEpoch)));
}
