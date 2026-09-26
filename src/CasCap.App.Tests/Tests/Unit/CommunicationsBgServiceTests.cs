namespace CasCap.Tests.Unit;

/// <summary>
/// Orchestration tests for <see cref="CommunicationsBgService"/>: envelope admission, duplicate
/// suppression, durable attachments, voice-processing modes and reply-queue backpressure.
/// </summary>
/// <remarks>
/// Every test drives the real <see cref="CommunicationsBgService.ExecuteAsync"/> pipeline and
/// observes it through <see cref="FakeSignalizrClient"/>; nothing reaches inference, Redis or Signalizr.
/// </remarks>
[Trait("Category", "Messaging")]
public class CommunicationsBgServiceTests
{
    private const string Eyes = "\U0001F440";
    private const string Ear = "\U0001F442";
    private const string Hourglass = "\u23F3";
    private const string RedCross = "\u274C";
    private const string WavMediaType = "audio/wav";

    [Fact]
    public async Task EarIsSentBeforeTranscriptionCompletes()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(VoiceProcessingMode.Enabled);
        var gate = new TaskCompletionSource();
        fixture.SpeechToText.Gate = gate;
        await fixture.StartAsync();

        fixture.Enqueue(CommunicationsBgServiceTestFixture.AttachmentEnvelope(7_601, ("voice-6", WavMediaType)));

        //The ear must land while the backend is still held, not after it returns.
        await CommunicationsBgServiceTestFixture.WaitForAsync(() => fixture.Signalizr.ReactionCount(Ear) == 1);
        Assert.Equal(0, fixture.Signalizr.ReactionCount(Hourglass));

        gate.SetResult();
        await CommunicationsBgServiceTestFixture.WaitForAsync(() => fixture.Signalizr.StartTypingCallCount == 1);
    }

    [Fact]
    public async Task VoiceFailureIsMarkedWithARedCross()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(VoiceProcessingMode.Enabled);
        fixture.SpeechToText.Failure = new HttpRequestException("backend down");
        await fixture.StartAsync();

        fixture.Enqueue(CommunicationsBgServiceTestFixture.AttachmentEnvelope(7_701, ("voice-7", WavMediaType)));

        await CommunicationsBgServiceTestFixture.WaitForAsync(() => fixture.Signalizr.ReactionCount(RedCross) == 1);
        var reply = Assert.Single(fixture.Signalizr.Sent);
        Assert.Contains("could not understand", reply.Message, StringComparison.Ordinal);
    }


    [Theory]
    [InlineData(VoiceProcessingMode.Disabled)]
    [InlineData(VoiceProcessingMode.Shadow)]
    [InlineData(VoiceProcessingMode.Enabled)]
    public async Task TextMessage_ReachesReplyQueueInEveryVoiceMode(VoiceProcessingMode mode)
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(mode);
        await fixture.StartAsync();

        fixture.Enqueue(CommunicationsBgServiceTestFixture.TextEnvelope("turn the kitchen light on", 1_001));

        //The drain loop only starts processing what the bounded channel actually accepted.
        await CommunicationsBgServiceTestFixture.WaitForAsync(() => fixture.Signalizr.StartTypingCallCount == 1);

        Assert.Equal(1, fixture.Signalizr.ReactionCount(Eyes));
        Assert.Equal(1_001, Assert.Single(fixture.Deduplicator.Claims).Timestamp);
        Assert.Empty(fixture.Signalizr.AttachmentFetches);
    }

    [Fact]
    public async Task ForeignGroupAndOwnEchoAreIgnored()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture();
        await fixture.StartAsync();

        fixture.Enqueue(
            CommunicationsBgServiceTestFixture.TextEnvelope("from another group", 2_001,
                groupId: CommunicationsBgServiceTestFixture.OtherChannel),
            CommunicationsBgServiceTestFixture.TextEnvelope("my own echo", 2_002,
                sender: CommunicationsBgServiceTestFixture.Account),
            CommunicationsBgServiceTestFixture.TextEnvelope("a real message", 2_003));

        await CommunicationsBgServiceTestFixture.WaitForAsync(() => fixture.Signalizr.StartTypingCallCount == 1);
        await CommunicationsBgServiceTestFixture.AssertStaysFalseAsync(() => fixture.Deduplicator.Claims.Count > 1);

        Assert.Equal(2_003, Assert.Single(fixture.Deduplicator.Claims).Timestamp);
    }

    [Fact]
    public async Task EnvelopesQueuedBeforeStartupAreProcessed()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture();
        //Queued before execution begins, so the startup flush drains it rather than the poll loop.
        fixture.Enqueue(CommunicationsBgServiceTestFixture.TextEnvelope("queued while we were down", 3_001));

        await fixture.StartAsync();

        await CommunicationsBgServiceTestFixture.WaitForAsync(() => fixture.Signalizr.StartTypingCallCount == 1);
        Assert.Equal(3_001, Assert.Single(fixture.Deduplicator.Claims).Timestamp);
    }

    [Fact]
    public async Task OnlySelectedDurableAttachmentIsDownloaded()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture();
        await fixture.StartAsync();

        fixture.Enqueue(CommunicationsBgServiceTestFixture.AttachmentEnvelope(4_001,
            ("a1", "image/jpeg"), ("a2", "image/png"), ("a3", WavMediaType)));

        await CommunicationsBgServiceTestFixture.WaitForAsync(() => !fixture.Signalizr.AttachmentFetches.IsEmpty);

        //Selection is deterministic: only the first identifier is downloaded.
        Assert.Equal(["a1"], fixture.Signalizr.AttachmentFetches);
    }

    [Fact]
    public async Task DuplicateClaimSuppressesReprocessing()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture();
        fixture.Deduplicator.ClaimResult = false;
        await fixture.StartAsync();

        fixture.Enqueue(CommunicationsBgServiceTestFixture.TextEnvelope("redelivered", 6_001));

        await CommunicationsBgServiceTestFixture.WaitForAsync(() => !fixture.Deduplicator.Claims.IsEmpty);
        await CommunicationsBgServiceTestFixture.AssertStaysFalseAsync(
            () => fixture.Signalizr.StartTypingCallCount > 0 || fixture.Signalizr.ReactionCount(Eyes) > 0);

        Assert.Empty(fixture.Signalizr.Sent);
    }

    [Fact]
    public async Task DisabledModeRejectsVoiceWithoutDownloading()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(VoiceProcessingMode.Disabled);
        await fixture.StartAsync();

        fixture.Enqueue(CommunicationsBgServiceTestFixture.AttachmentEnvelope(7_001, ("voice-1", WavMediaType)));

        await CommunicationsBgServiceTestFixture.WaitForAsync(() => !fixture.Deduplicator.Claims.IsEmpty);
        await CommunicationsBgServiceTestFixture.AssertStaysFalseAsync(
            () => fixture.Signalizr.StartTypingCallCount > 0 || !fixture.Signalizr.AttachmentFetches.IsEmpty);

        Assert.Empty(fixture.Signalizr.Sent);
    }

    [Fact]
    public async Task ShadowModeDownloadsButDoesNotReply()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(VoiceProcessingMode.Shadow);
        await fixture.StartAsync();

        fixture.Enqueue(CommunicationsBgServiceTestFixture.AttachmentEnvelope(7_101, ("voice-2", WavMediaType)));

        await CommunicationsBgServiceTestFixture.WaitForAsync(() => !fixture.Signalizr.AttachmentFetches.IsEmpty);
        await CommunicationsBgServiceTestFixture.AssertStaysFalseAsync(
            () => fixture.Signalizr.StartTypingCallCount > 0 || !fixture.Signalizr.Sent.IsEmpty);

        Assert.Equal(["voice-2"], fixture.Signalizr.AttachmentFetches);
        //Shadow still acknowledges that the note was heard; it just never replies.
        Assert.Equal(1, fixture.Signalizr.ReactionCount(Ear));
        Assert.Equal(0, fixture.Signalizr.ReactionCount(Eyes));
    }

    [Fact]
    public async Task EnabledModeCarriesVoiceThroughToTheReplyQueue()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(VoiceProcessingMode.Enabled);
        await fixture.StartAsync();

        fixture.Enqueue(CommunicationsBgServiceTestFixture.AttachmentEnvelope(7_201, ("voice-3", WavMediaType)));

        await CommunicationsBgServiceTestFixture.WaitForAsync(() => fixture.Signalizr.StartTypingCallCount == 1);

        Assert.Equal(["voice-3"], fixture.Signalizr.AttachmentFetches);
        //A voice note is acknowledged with an ear, not the eyes used for a text message.
        Assert.Equal(1, fixture.Signalizr.ReactionCount(Ear));
        Assert.Equal(0, fixture.Signalizr.ReactionCount(Eyes));
    }

    [Fact]
    public async Task TranscriptIsNotEchoedToTheMonitorChannelByDefault()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(VoiceProcessingMode.Enabled);
        await fixture.StartAsync();

        fixture.Enqueue(CommunicationsBgServiceTestFixture.AttachmentEnvelope(7_301, ("voice-4", WavMediaType)));

        await CommunicationsBgServiceTestFixture.WaitForAsync(() => fixture.Signalizr.StartTypingCallCount == 1);
        await CommunicationsBgServiceTestFixture.AssertStaysFalseAsync(
            () => fixture.Signalizr.SentTo(CommunicationsBgServiceTestFixture.MonitorChannel).Any());
    }

    [Fact]
    public async Task EnabledEchoSendsTheTranscriptToTheMonitorChannelOnly()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(VoiceProcessingMode.Enabled,
            echoTranscriptToDebugChat: true);
        fixture.SpeechToText.Transcript = "turn the kitchen lights off";
        await fixture.StartAsync();

        fixture.Enqueue(CommunicationsBgServiceTestFixture.AttachmentEnvelope(7_401, ("voice-5", WavMediaType)));

        await CommunicationsBgServiceTestFixture.WaitForAsync(
            () => fixture.Signalizr.SentTo(CommunicationsBgServiceTestFixture.MonitorChannel).Any());

        var debugMsg = Assert.Single(fixture.Signalizr.SentTo(CommunicationsBgServiceTestFixture.MonitorChannel));
        Assert.Contains("turn the kitchen lights off", debugMsg.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Signalizr.SentTo(CommunicationsBgServiceTestFixture.ChatChannel),
            sent => sent.Message.Contains("turn the kitchen lights off", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaturatedReplyQueueMakesTheProducerWait()
    {
        await using var fixture = new CommunicationsBgServiceTestFixture(replyQueueCapacity: 1);
        await fixture.StartAsync();

        //Hold the single reader inside the first reply so the bounded queue cannot drain.
        fixture.Signalizr.BlockStartTyping();

        fixture.Enqueue(
            CommunicationsBgServiceTestFixture.TextEnvelope("first", 8_001),
            CommunicationsBgServiceTestFixture.TextEnvelope("second", 8_002),
            CommunicationsBgServiceTestFixture.TextEnvelope("third", 8_003),
            CommunicationsBgServiceTestFixture.TextEnvelope("fourth", 8_004));

        //One reply is held by the reader, one occupies the queue, and the third producer blocks
        //on admission — so the fourth envelope is never even acknowledged.
        await CommunicationsBgServiceTestFixture.WaitForAsync(() => fixture.Signalizr.ReactionCount(Eyes) == 3);
        await CommunicationsBgServiceTestFixture.AssertStaysFalseAsync(() => fixture.Signalizr.ReactionCount(Eyes) > 3);
        Assert.Equal(3, fixture.Deduplicator.Claims.Count);

        fixture.Signalizr.ReleaseStartTyping();

        //Nothing accepted was evicted: the waiting producer resumes and the backlog clears.
        await CommunicationsBgServiceTestFixture.WaitForAsync(() => fixture.Signalizr.ReactionCount(Eyes) == 4);
        Assert.Equal(4, fixture.Deduplicator.Claims.Count);
        await CommunicationsBgServiceTestFixture.WaitForAsync(() => fixture.Signalizr.StartTypingCallCount == 4);
    }
}
