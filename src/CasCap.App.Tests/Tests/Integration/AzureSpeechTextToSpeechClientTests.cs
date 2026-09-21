using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CasCap.Tests;

//ITextToSpeechClient and its options are published as experimental (MEAI001); suppressed here as in
//the adapters under test rather than repository-wide.
#pragma warning disable MEAI001

/// <summary>
/// Exercises <see cref="AzureSpeechTextToSpeechClient"/> against the real Azure AI Speech resource.
/// </summary>
/// <remarks>
/// Asserts on the container signature rather than only on a byte count, because a provider that
/// quietly returns a different format still returns plausible-looking bytes, and the reply would
/// then arrive as an unplayable file rather than a voice note.
/// <para>
/// Skipped unless the Azure provider is configured and a credential is available, which is how it
/// stays green on a machine with no Azure access.
/// </para>
/// </remarks>
[Trait("Category", "TextToSpeech")]
[Trait("Category", "Integration")]
public class AzureSpeechTextToSpeechClientTests : TestBase
{
    public AzureSpeechTextToSpeechClientTests(ITestOutputHelper output) : base(output) { }

    private const string ReplyText = "The heating is on and the hallway door is closed.";

    [Fact]
    public async Task GetAudioAsync_ReturnsPlayableOggOpus()
    {
        var textToSpeech = _configuration.GetSection(TextToSpeechConfig.ConfigurationSectionName)
            .Get<TextToSpeechConfig>();
        var speechToText = _configuration.GetSection(SpeechToTextConfig.ConfigurationSectionName)
            .Get<SpeechToTextConfig>();
        var auth = _configuration.GetSection(AzureAuthConfig.ConfigurationSectionName).Get<AzureAuthConfig>();

        var endpoint = textToSpeech?.AzureSpeechEndpoint ?? speechToText?.AzureEndpoint;
        Assert.SkipWhen(string.IsNullOrWhiteSpace(endpoint),
            $"{nameof(TextToSpeechConfig.AzureSpeechEndpoint)} is not configured here.");
        Assert.SkipWhen(auth?.TokenCredential is null,
            "No Azure credential is available here; the edge service principal certificate is required.");

        var speechService = new SpeechService(new Uri(endpoint!), auth!.TokenCredential!);
        var sut = new AzureSpeechTextToSpeechClient(NullLogger<AzureSpeechTextToSpeechClient>.Instance,
            speechService, Options.Create(textToSpeech ?? new TextToSpeechConfig()));

        var response = await sut.GetAudioAsync(ReplyText, options: null, TestContext.Current.CancellationToken);

        var audio = Assert.Single(response.Contents.OfType<DataContent>());
        _output.WriteLine($"mediaType: {audio.MediaType}, bytes: {audio.Data.Length}");

        Assert.False(audio.Data.IsEmpty, "Synthesis returned no audio.");
        Assert.Equal(AzureSpeechTextToSpeechClient.OggOpusMediaType, audio.MediaType);

        //"OggS" is the Ogg page header; Opus in a WAV or MP3 wrapper would not carry it.
        var signature = System.Text.Encoding.ASCII.GetString(audio.Data.Span[..4]);
        Assert.True(signature is "OggS",
            $"Expected an Ogg container but the payload begins with '{signature}'.");

        //Written out so the audio can actually be listened to; a passing assertion says nothing
        //  about whether the voice is any good.
        var path = Path.Combine(OutputDirectory, $"azure-speech-{textToSpeech?.AzureSpeechVoice ?? "default"}.ogg");
        Directory.CreateDirectory(OutputDirectory);
        await File.WriteAllBytesAsync(path, audio.Data.ToArray(), TestContext.Current.CancellationToken);
        _output.WriteLine($"saved: {path}");
    }

    /// <summary>Where synthesized samples are written for listening.</summary>
    internal static string OutputDirectory { get; } =
        Path.Combine(Path.GetTempPath(), "smarthaus-tts");
}
