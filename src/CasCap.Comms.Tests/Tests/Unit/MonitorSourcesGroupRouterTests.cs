namespace CasCap.Tests.Unit;

/// <summary>Verifies which Signalizr group <see cref="MonitorSourcesGroupRouter"/> chooses for a comms stream event.</summary>
[Trait("Category", "Comms")]
public sealed class MonitorSourcesGroupRouterTests
{
    private const string ChatGroup = "My Test Group Name";
    private const string MonitorGroup = "My Test Monitor Group Name";

    [Theory]
    [InlineData("DDnsBgService", MonitorGroup)]
    [InlineData("SecurityAgent", ChatGroup)]
    public void ResolveGroup_ConfiguredMonitorSource_GoesToMonitorGroup(string source, string expectedGroup)
    {
        var router = CreateRouter(MonitorGroup, "DDnsBgService");

        Assert.Equal(expectedGroup, router.ResolveGroup(CreateEvent(source)));
    }

    [Fact]
    public void ResolveGroup_NoMonitorGroup_KeepsEveryEventInChatGroup()
    {
        var router = CreateRouter(monitorGroup: null, "DDnsBgService");

        Assert.Equal(ChatGroup, router.ResolveGroup(CreateEvent("DDnsBgService")));
    }

    private static MonitorSourcesGroupRouter CreateRouter(string? monitorGroup, params string[] monitorSources) =>
        new(Options.Create(new CommsConfig
        {
            GroupName = ChatGroup,
            MonitorGroupName = monitorGroup,
            MonitorSources = [.. monitorSources],
        }));

    private static CommsEvent CreateEvent(string source) => new()
    {
        Source = source,
        Message = "event",
        TimestampUtc = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc),
        Environment = "dev",
    };
}
