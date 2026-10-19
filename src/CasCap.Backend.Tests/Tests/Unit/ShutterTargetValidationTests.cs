using CasCap.Models.Dtos;

namespace CasCap.Tests.Unit;

/// <summary>Regression tests for rejecting shutter moves that the KNX export cannot carry out.</summary>
[Trait("Category", "Knx")]
public sealed class ShutterTargetValidationTests
{
    private static readonly KnxGroupAddressGroup[] Groups =
    [
        CreateBlind("DG-BL-Office-South", "MOVE", "STEP"),
        CreateBlind("DG-BL-Office-South-L", "MOVE", "POS", "POS_FB", "POSSLATS"),
        CreateBlind("DG-BL-Office-South-R", "MOVE", "POS", "POS_FB", "POSSLATS"),
    ];

    [Fact]
    public void ValidateShutterTarget_AcceptsIndividualBlind()
    {
        var request = new KnxShutterStateChangeRequest { GroupName = "DG-BL-Office-South-L", VPosition = 100, Slats = 100 };

        Assert.Null(BusSystemMcpQueryService.ValidateShutterTarget(Groups, request));
    }

    // Regression: a move sent to a group without POS was reported as started but never reached the bus.
    [Fact]
    public void ValidateShutterTarget_RejectsGroupWithoutPosition_AndNamesIndividualBlinds()
    {
        var request = new KnxShutterStateChangeRequest { GroupName = "DG-BL-Office-South", VPosition = 100 };

        var error = BusSystemMcpQueryService.ValidateShutterTarget(Groups, request);

        Assert.NotNull(error);
        Assert.Contains("DG-BL-Office-South-L", error);
        Assert.Contains("DG-BL-Office-South-R", error);
    }

    [Fact]
    public void ValidateShutterTarget_RejectsUnknownBlind()
    {
        var request = new KnxShutterStateChangeRequest { GroupName = "DG-BL-Missing", VPosition = 0 };

        var error = BusSystemMcpQueryService.ValidateShutterTarget(Groups, request);

        Assert.NotNull(error);
        Assert.Contains("not a known blind", error);
    }

    private static KnxGroupAddressGroup CreateBlind(string groupName, params string[] functions) => new()
    {
        GroupName = groupName,
        Category = GroupAddressCategory.BL,
        Children =
        [
            .. functions.Select((f, i) => new KnxGroupAddressGroupFunction
            {
                Name = $"{groupName}-{f}",
                GroupAddress = $"1/2/{i}",
                Function = f,
                DPTs = "DPST-5-1",
            }),
        ],
    };
}
