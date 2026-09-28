using static CasCap.Tests.Unit.CommunicationsBgServiceTestFixture;

namespace CasCap.Tests.Unit;

/// <summary>
/// Stream-path tests for <see cref="CommunicationsBgService"/>: direct delivery, routing, flood protection,
/// cached media, startup and resubscription, plus the no-responder inbound behaviour.
/// </summary>
/// <remarks>Every test drives the real pipeline over in-memory Signalizr and Redis fakes.</remarks>
[Trait("Category", "Comms")]
public sealed class CommunicationsBgServiceStreamTests
{
    private const string TradeSource = "TradeEventSinkCommsService";
    private const string SchedulerSource = "SchedulerBgService";

    [Fact]
    public async Task StreamEvent_WithoutResponder_IsFormattedAndSentToChatGroup()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(responderEnabled: false,
            formatter: new TimestampedCommsEventFormatter());
        await fixture.StartAsync();
        var timestamp = DateTime.UtcNow;

        fixture.AddStreamEvent(TradeSource, "ORDER placed", timestamp);

        await WaitForAsync(() => fixture.Signalizr.SentTo(ChatGroupName).Any());
        var sent = Assert.Single(fixture.Signalizr.Sent);
        Assert.Equal($"{timestamp:HH:mm:ss.fff} UTC [{EnvironmentAcronym}] [{TradeSource}] ORDER placed", sent.Message);
        Assert.Null(sent.Attachments);
        await WaitForAsync(() => fixture.Redis.Acknowledged.Count == 1);
    }

    [Fact]
    public async Task StreamEvent_WithResponder_BecomesATurnAndIsCopiedToMonitorGroup()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture();
        await fixture.StartAsync();

        fixture.AddStreamEvent(TradeSource, "ORDER placed", DateTime.UtcNow);

        //A stream turn has no sender, so it produces no typing indicator; the stub agent then fails it.
        await WaitForAsync(() => fixture.Signalizr.SentTo(MonitorGroupName).Any());
        Assert.Contains(TradeSource, Assert.Single(fixture.Signalizr.SentTo(MonitorGroupName)).Message, StringComparison.Ordinal);
        await AssertStaysFalseAsync(() => fixture.Signalizr.SentTo(ChatGroupName).Any());
    }

    [Fact]
    public async Task MonitorSource_IsSentDirectlyToMonitorGroupEvenWithResponder()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(monitorSources: [SchedulerSource]);
        await fixture.StartAsync();

        fixture.AddStreamEvent(SchedulerSource, "snapshot complete", DateTime.UtcNow);

        await WaitForAsync(() => fixture.Signalizr.SentTo(MonitorGroupName).Any());
        Assert.Equal("snapshot complete", Assert.Single(fixture.Signalizr.SentTo(MonitorGroupName)).Message);
        Assert.Empty(fixture.Signalizr.SentTo(ChatGroupName));
    }

    [Fact]
    public async Task StaleEvent_IsDroppedWithMonitorNotice()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(responderEnabled: false);
        await fixture.StartAsync();

        fixture.AddStreamEvent(TradeSource, "late order", DateTime.UtcNow.AddMinutes(-10));

        await WaitForAsync(() => fixture.Signalizr.SentTo(MonitorGroupName).Any());
        Assert.Contains("older than", Assert.Single(fixture.Signalizr.SentTo(MonitorGroupName)).Message, StringComparison.Ordinal);
        Assert.Empty(fixture.Signalizr.SentTo(ChatGroupName));
        await WaitForAsync(() => fixture.Redis.Acknowledged.Count == 1);
    }

    [Fact]
    public async Task BurstBeyondThrottle_IsDroppedWithMonitorNotice()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(responderEnabled: false,
            streamSendBurst: 1, streamSendRatePerMinute: 1);
        await fixture.StartAsync();

        fixture.AddStreamEvent(TradeSource, "first", DateTime.UtcNow);
        fixture.AddStreamEvent(TradeSource, "second", DateTime.UtcNow);

        await WaitForAsync(() => fixture.Signalizr.SentTo(MonitorGroupName).Any());
        Assert.Contains("rate limit", Assert.Single(fixture.Signalizr.SentTo(MonitorGroupName)).Message, StringComparison.Ordinal);
        Assert.Equal("first", Assert.Single(fixture.Signalizr.SentTo(ChatGroupName)).Message);
        await WaitForAsync(() => fixture.Redis.Acknowledged.Count == 2);
    }

    [Fact]
    public async Task MediaReference_IsAttachedAndDeleted()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(responderEnabled: false);
        await fixture.StartAsync();
        const string mediaKey = "comms:cache:media:test";
        fixture.Redis.Strings[mediaKey] = [1, 2, 3];
        var payload = JsonSerializer.Serialize(new MediaReference { MediaRedisKey = mediaKey, MimeType = "image/png", FileName = "chart.png" });

        fixture.AddStreamEvent(TradeSource, "chart", DateTime.UtcNow, payload);

        await WaitForAsync(() => fixture.Signalizr.SentTo(ChatGroupName).Any());
        var attachments = Assert.Single(fixture.Signalizr.Sent).Attachments;
        Assert.NotNull(attachments);
        Assert.Equal("data:image/png;filename=chart.png;base64,AQID", Assert.Single(attachments));
        Assert.Contains(mediaKey, fixture.Redis.DeletedKeys);
    }

    [Fact]
    public async Task NonMediaPayload_IsSentWithoutAttachment()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(responderEnabled: false);
        await fixture.StartAsync();

        fixture.AddStreamEvent(TradeSource, "context", DateTime.UtcNow, """{"Symbol":"EURUSD"}""");

        await WaitForAsync(() => fixture.Signalizr.SentTo(ChatGroupName).Any());
        Assert.Null(Assert.Single(fixture.Signalizr.Sent).Attachments);
    }

    [Fact]
    public async Task UnreachableGateway_IsRetriedBeforeDelivery()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(responderEnabled: false);
        fixture.Signalizr.GroupsFailures = 3;
        fixture.LaunchWithoutWaiting();

        fixture.AddStreamEvent(TradeSource, "queued while offline", DateTime.UtcNow);

        await WaitForAsync(() => fixture.Signalizr.SentTo(ChatGroupName).Any());
        Assert.Equal(4, fixture.Signalizr.GetGroupsCallCount);
    }

    [Fact]
    public async Task MissingChatGroup_FaultsTheFeature()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(responderEnabled: false);
        fixture.Signalizr.Groups = [MonitorGroupName];

        fixture.LaunchWithoutWaiting();

        var ex = await Assert.ThrowsAsync<CasCap.Signalizr.Client.Exceptions.SignalizrGroupNotConfiguredException>(
            () => fixture.Execution);
        Assert.Equal(1, ex.MissingGroupCount);
    }

    [Fact]
    public async Task CleanSubscriptionEnd_IsResubscribed()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(responderEnabled: false);
        await fixture.StartAsync();

        fixture.Signalizr.Complete();

        await WaitForAsync(() => fixture.Signalizr.SubscribeCallCount >= 2);
    }

    [Fact]
    public async Task InboundMessage_WithoutResponder_IsIgnoredSilently()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(responderEnabled: false);
        await fixture.StartAsync();

        fixture.Enqueue(TextEnvelope("what trades are open?", 9_001));

        await AssertStaysFalseAsync(() => !fixture.Signalizr.Reactions.IsEmpty
            || !fixture.Signalizr.Sent.IsEmpty
            || !fixture.Deduplicator.Claims.IsEmpty
            || fixture.Signalizr.StartTypingCallCount > 0);
    }

    [Theory]
    [InlineData(false, ChatGroupName, "what trades are open?", true)]
    [InlineData(true, ChatGroupName, "what trades are open?", false)]
    [InlineData(false, MonitorGroupName, "what trades are open?", false)]
    [InlineData(false, ChatGroupName, " ", false)]
    public async Task ShouldProcessMessage_AcceptsOnlyOthersContentInChatGroup(bool fromSelf, string groupName, string text,
        bool expected)
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(responderEnabled: false);
        var message = new SignalizrMessage
        {
            DeliveryId = "d1",
            GroupName = groupName,
            Sender = Sender,
            Message = text,
            Timestamp = 1,
            FromSelf = fromSelf,
        };

        Assert.Equal(expected, fixture.Service.ShouldProcessMessage(message));
    }
}
