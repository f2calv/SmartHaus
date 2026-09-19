using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

namespace CasCap.Tests;

//ITextToSpeechClient and its options are published as experimental (MEAI001); suppressed here as in
//the adapter under test rather than repository-wide.
#pragma warning disable MEAI001

/// <summary>
/// Exercises <see cref="PiperTextToSpeechClient"/> against a running Piper server.
/// </summary>
/// <remarks>
/// Needs both a reachable Wyoming socket and a working ffmpeg, so it is skipped rather than failed
/// when either is absent. Start one locally with:
/// <c>docker run -d --name piper -e PIPER_VOICE=en_GB-alba-medium -p 10200:10200 lscr.io/linuxserver/piper</c>.
/// </remarks>
[Trait("Category", "TextToSpeech")]
public class PiperTextToSpeechClientTests : TestBase
{
    public PiperTextToSpeechClientTests(ITestOutputHelper output) : base(output) { }

    private const string ReplyText = "The heating is on and the hallway door is closed.";

    [Fact]
    public async Task GetAudioAsync_ReturnsPlayableOggOpus()
    {
        var textToSpeech = _configuration.GetSection(TextToSpeechConfig.ConfigurationSectionName)
            .Get<TextToSpeechConfig>() ?? new TextToSpeechConfig();

        //Falls back to a local container so the test is useful before the server is deployed.
        var endpoint = textToSpeech.PiperEndpoint ?? "localhost:10200";
        Assert.SkipUnless(await IsReachableAsync(endpoint), $"No Piper server is listening on {endpoint}.");
        //Piper emits raw PCM, so without ffmpeg this exercises only half the adapter.
        Assert.SkipUnless(IsFfmpegAvailable(textToSpeech.FfmpegPath),
            $"ffmpeg was not found at '{textToSpeech.FfmpegPath}'; it is required to encode Opus.");

        var config = textToSpeech with { PiperEndpoint = endpoint };
        var sut = new PiperTextToSpeechClient(NullLogger<PiperTextToSpeechClient>.Instance,
            Options.Create(config));

        var sw = Stopwatch.StartNew();
        var response = await sut.GetAudioAsync(ReplyText, options: null, TestContext.Current.CancellationToken);
        sw.Stop();

        var audio = Assert.Single(response.Contents.OfType<DataContent>());
        _output.WriteLine($"endpoint: {endpoint}, voice: {config.PiperVoice ?? "server default"}");
        _output.WriteLine($"mediaType: {audio.MediaType}, bytes: {audio.Data.Length}, elapsed: {sw.ElapsedMilliseconds}ms");

        Assert.False(audio.Data.IsEmpty, "Synthesis returned no audio.");
        Assert.Equal(PiperTextToSpeechClient.OggOpusMediaType, audio.MediaType);

        var signature = System.Text.Encoding.ASCII.GetString(audio.Data.Span[..4]);
        Assert.True(signature is "OggS",
            $"Expected an Ogg container but the payload begins with '{signature}'. "
            + "ffmpeg may have produced a different format.");

        var path = Path.Combine(AzureSpeechTextToSpeechClientTests.OutputDirectory,
            $"piper-adapter-{config.PiperVoice ?? "default"}.ogg");
        Directory.CreateDirectory(AzureSpeechTextToSpeechClientTests.OutputDirectory);
        await File.WriteAllBytesAsync(path, audio.Data.ToArray(), TestContext.Current.CancellationToken);
        _output.WriteLine($"saved: {path}");
    }

    private static bool IsFfmpegAvailable(string ffmpegPath)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(ffmpegPath, "-version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (process is null)
                return false;
            process.WaitForExit(5_000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<bool> IsReachableAsync(string endpoint)
    {
        var (host, port) = PiperTextToSpeechClient.ParseEndpoint(endpoint);
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(host, port, timeout.Token);
            return client.Connected;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
