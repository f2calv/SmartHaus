namespace CasCap.Tests;

/// <summary>
/// Exercises <see cref="AzureSpeechToTextClient"/> against the real Azure AI Speech resource.
/// </summary>
/// <remarks>
/// Speech is synthesized first and then transcribed back, so the test needs no committed audio
/// fixture and no recording of a real voice. That does mean a failure implicates either direction,
/// so read the assertion message before assuming transcription is at fault.
/// <para>
/// Skipped unless the Azure provider is configured and a credential is available, which is how it
/// stays green on a machine with no Azure access.
/// </para>
/// </remarks>
[Trait("Category", "SpeechToText")]
public class AzureSpeechToTextClientTests : TestBase
{
    public AzureSpeechToTextClientTests(ITestOutputHelper output) : base(output) { }

    //Distinctive, unambiguous words; a transcript of this should not drift between model versions.
    private const string SpokenText = "The quick brown fox jumps over the lazy dog.";

    [Fact]
    public async Task TranscribeAsync_ReturnsTheSynthesizedSentence()
    {
        var speechToText = _configuration.GetSection(SpeechToTextConfig.ConfigurationSectionName)
            .Get<SpeechToTextConfig>();
        var auth = _configuration.GetSection(AzureAuthConfig.ConfigurationSectionName).Get<AzureAuthConfig>();

        Assert.SkipWhen(string.IsNullOrWhiteSpace(speechToText?.AzureEndpoint),
            $"{nameof(SpeechToTextConfig.AzureEndpoint)} is not configured here.");
        Assert.SkipWhen(auth?.TokenCredential is null,
            "No Azure credential is available here; the edge service principal certificate is required.");

        var sut = new SpeechService(new Uri(speechToText!.AzureEndpoint!), auth!.TokenCredential!);
        var wavPath = Path.Combine(Path.GetTempPath(), $"{nameof(AzureSpeechToTextClientTests)}-{Guid.NewGuid():N}.wav");
        try
        {
            await sut.CreateWAV(SpokenText, wavPath);
            Assert.True(File.Exists(wavPath), "Speech synthesis produced no audio to transcribe.");

            await using var audio = File.OpenRead(wavPath);
            var transcript = await sut.TranscribeAsync(audio, ["en-GB"], TestContext.Current.CancellationToken);

            _output.WriteLine($"transcript: {transcript}");
            Assert.False(string.IsNullOrWhiteSpace(transcript), "No speech was recognized in the synthesized audio.");
            //Punctuation and casing vary by model, so compare on the words that carry the meaning.
            Assert.Contains("quick brown fox", transcript, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(wavPath);
        }
    }
}
