using Microsoft.Extensions.Logging.Abstractions;

namespace CasCap.Tests.Unit;

//ITextToSpeechClient is published as experimental (MEAI001); see AzureSpeechTextToSpeechClient.
#pragma warning disable MEAI001

/// <summary>
/// Policy and failure-path tests for <see cref="VoiceReplySynthesisService"/>.
/// </summary>
[Trait("Category", "TextToSpeech")]
public class VoiceReplySynthesisServiceTests
{
    private const string _reply = "The hallway door is closed.";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Disabled_NeverSynthesizes(bool inboundWasVoice)
    {
        var tts = new FakeTextToSpeechClient();
        var svc = CreateService(tts, VoiceReplyMode.Disabled);

        var result = await svc.TrySynthesizeAsync(_reply, inboundWasVoice,
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(0, tts.Requests);
    }

    [Fact]
    public async Task MatchInbound_TextMessage_DoesNotSynthesize()
    {
        var tts = new FakeTextToSpeechClient();
        var svc = CreateService(tts, VoiceReplyMode.MatchInbound);

        var result = await svc.TrySynthesizeAsync(_reply, inboundWasVoice: false,
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(0, tts.Requests);
    }

    [Fact]
    public async Task MatchInbound_VoiceMessage_ReturnsAttachment()
    {
        var tts = new FakeTextToSpeechClient();
        var svc = CreateService(tts, VoiceReplyMode.MatchInbound);

        var result = await svc.TrySynthesizeAsync(_reply, inboundWasVoice: true,
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("audio/ogg", result.MediaType);
        Assert.Equal(VoiceReplySynthesisService.AttachmentFileName, result.FileName);
        Assert.NotEmpty(result.Audio.ToArray());
        Assert.Equal(_reply, tts.LastText);
    }

    [Fact]
    public async Task Always_TextMessage_ReturnsAttachment()
    {
        var tts = new FakeTextToSpeechClient();
        var svc = CreateService(tts, VoiceReplyMode.Always);

        var result = await svc.TrySynthesizeAsync(_reply, inboundWasVoice: false,
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(1, tts.Requests);
    }

    [Fact]
    public async Task ReplyLongerThanMaxCharacters_DoesNotSynthesize()
    {
        var tts = new FakeTextToSpeechClient();
        var svc = CreateService(tts, VoiceReplyMode.Always, maxCharacters: 10);

        var result = await svc.TrySynthesizeAsync(new string('a', 11), inboundWasVoice: true,
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(0, tts.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyReply_DoesNotSynthesize(string? text)
    {
        var tts = new FakeTextToSpeechClient();
        var svc = CreateService(tts, VoiceReplyMode.Always);

        var result = await svc.TrySynthesizeAsync(text, inboundWasVoice: true,
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(0, tts.Requests);
    }

    //A spoken reply is decoration; losing it must never cost the sender their text reply.
    [Fact]
    public async Task BackendFailure_IsContained()
    {
        var tts = new FakeTextToSpeechClient { Failure = new HttpRequestException("backend down") };
        var svc = CreateService(tts, VoiceReplyMode.Always);

        var result = await svc.TrySynthesizeAsync(_reply, inboundWasVoice: true,
            TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task EmptyAudio_IsTreatedAsNoAttachment()
    {
        var tts = new FakeTextToSpeechClient { Audio = [] };
        var svc = CreateService(tts, VoiceReplyMode.Always);

        var result = await svc.TrySynthesizeAsync(_reply, inboundWasVoice: true,
            TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    private static VoiceReplySynthesisService CreateService(FakeTextToSpeechClient tts, VoiceReplyMode mode,
        int maxCharacters = 1_000) =>
        new(NullLogger<VoiceReplySynthesisService>.Instance, tts,
            Options.Create(new TextToSpeechConfig { Mode = mode, MaxCharacters = maxCharacters }));
}
