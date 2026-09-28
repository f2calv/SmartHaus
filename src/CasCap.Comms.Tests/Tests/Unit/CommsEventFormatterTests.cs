namespace CasCap.Tests.Unit;

/// <summary>Tests the text used when a comms stream event is forwarded directly to a Signalizr group.</summary>
[Trait("Category", "Comms")]
public sealed class CommsEventFormatterTests
{
    [Fact]
    public void TimestampedFormatter_PrefixesTimeEnvironmentAndSource()
    {
        var commsEvent = new CommsEvent
        {
            Source = "SchedulerBgService",
            Message = "snapshot complete",
            TimestampUtc = new DateTime(2026, 9, 28, 4, 5, 6, 789, DateTimeKind.Utc),
            Environment = "dev",
        };

        var text = new TimestampedCommsEventFormatter().Format(commsEvent);

        Assert.Equal("04:05:06.789 UTC [dev] [SchedulerBgService] snapshot complete", text);
    }

    [Fact]
    public void PlainFormatter_ForwardsTheMessageUnchanged()
    {
        var commsEvent = new CommsEvent
        {
            Source = "SchedulerBgService",
            Message = "snapshot complete",
            TimestampUtc = new DateTime(2026, 9, 28, 4, 5, 6, 789, DateTimeKind.Utc),
            Environment = "dev",
        };

        Assert.Equal("snapshot complete", new PlainCommsEventFormatter().Format(commsEvent));
    }
}
