using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

namespace CasCap.Tests;

//ITextToSpeechClient and its options are published as experimental (MEAI001); suppressed here as in
//the adapters under test rather than repository-wide.
#pragma warning disable MEAI001

/// <summary>
/// Exercises <see cref="AzureOpenAiTextToSpeechClient"/> against the real Azure OpenAI audio
/// deployment.
/// </summary>
/// <remarks>
/// Reports elapsed time and writes the audio out, because the deployment need not be co-located with
/// the Speech resource and transport is expected to dominate the comparison between the two.
/// <para>
/// Skipped unless the deployment is configured and a credential is available.
/// </para>
/// </remarks>
[Trait("Category", "TextToSpeech")]
[Trait("Category", "Integration")]
public class AzureOpenAiTextToSpeechClientTests : TestBase
{
    public AzureOpenAiTextToSpeechClientTests(ITestOutputHelper output) : base(output) { }

    private const string ReplyText = "The heating is on and the hallway door is closed.";

    [Fact]
    public async Task GetAudioAsync_ReturnsPlayableOggOpus()
    {
        var textToSpeech = _configuration.GetSection(TextToSpeechConfig.ConfigurationSectionName)
            .Get<TextToSpeechConfig>();
        var auth = _configuration.GetSection(AzureAuthConfig.ConfigurationSectionName).Get<AzureAuthConfig>();

        Assert.SkipWhen(string.IsNullOrWhiteSpace(textToSpeech?.AzureOpenAiEndpoint),
            $"{nameof(TextToSpeechConfig.AzureOpenAiEndpoint)} is not configured here.");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(textToSpeech?.AzureOpenAiDeployment),
            $"{nameof(TextToSpeechConfig.AzureOpenAiDeployment)} is not configured here.");
        Assert.SkipWhen(auth?.TokenCredential is null,
            "No Azure credential is available here; the edge service principal certificate is required.");

        //A real factory rather than the unit-test stub, whose one-second timeout would abort the call.
        var httpClientFactory = new ServiceCollection().AddHttpClient()
            .BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
        var sut = new AzureOpenAiTextToSpeechClient(NullLogger<AzureOpenAiTextToSpeechClient>.Instance,
            Options.Create(textToSpeech!), Options.Create(auth!), httpClientFactory);

        var sw = Stopwatch.StartNew();
        var response = await sut.GetAudioAsync(ReplyText, options: null, TestContext.Current.CancellationToken);
        sw.Stop();

        var audio = Assert.Single(response.Contents.OfType<DataContent>());
        _output.WriteLine($"deployment: {textToSpeech!.AzureOpenAiDeployment}, voice: {textToSpeech.AzureOpenAiVoice ?? "default"}");
        _output.WriteLine($"mediaType: {audio.MediaType}, bytes: {audio.Data.Length}, elapsed: {sw.ElapsedMilliseconds}ms");

        Assert.False(audio.Data.IsEmpty, "Synthesis returned no audio.");

        var signature = System.Text.Encoding.ASCII.GetString(audio.Data.Span[..4]);
        Assert.True(signature is "OggS",
            $"Expected an Ogg container but the payload begins with '{signature}'.");

        var path = Path.Combine(AzureSpeechTextToSpeechClientTests.OutputDirectory,
            $"azure-openai-{textToSpeech.AzureOpenAiVoice ?? "default"}.ogg");
        Directory.CreateDirectory(AzureSpeechTextToSpeechClientTests.OutputDirectory);
        await File.WriteAllBytesAsync(path, audio.Data.ToArray(), TestContext.Current.CancellationToken);
        _output.WriteLine($"saved: {path}");
    }
}
