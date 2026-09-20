using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace CasCap.Tests.Unit;

//ISpeechToTextClient is published as experimental (MEAI001); see WhisperAsrSpeechToTextClient.
#pragma warning disable MEAI001

/// <summary>
/// Contract tests pinning the wire shape <see cref="WhisperAsrSpeechToTextClient"/> sends to an
/// openai-whisper-asr-webservice deployment, using a recording <see cref="HttpMessageHandler"/>.
/// </summary>
[Trait("Category", "SpeechToText")]
public class WhisperAsrSpeechToTextClientTests
{
    private const string _endpoint = "http://speech.example.com:9000";

    [Theory]
    [InlineData("audio/wav", "false", "audio.wav")]
    [InlineData("audio/x-wav", "false", "audio.wav")]
    [InlineData("audio/aac", "true", "audio.aac")]
    [InlineData("audio/ogg", "true", "audio.ogg")]
    [InlineData("audio/mp4", "true", "audio.m4a")]
    [InlineData("audio/mpeg", "true", "audio.mp3")]
    public async Task GetTextAsync_RequestContract(string mediaType, string expectedEncode, string expectedFileName)
    {
        using var handler = new RecordingHandler(JsonResponse("""{"text":"ok"}"""));
        using var client = CreateSpeechClient(handler);
        using var audio = new MemoryStream("payload"u8.ToArray());

        await client.GetTextAsync(audio, OptionsFor(mediaType), TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal($"{_endpoint}/asr", handler.RequestUri!.GetLeftPart(UriPartial.Path));
        Assert.Equal($"?task=transcribe&language=en&encode={expectedEncode}&output=json", handler.RequestUri.Query);
        //Quoting is left to HttpClient, which emits bare tokens; only the field names are our contract.
        Assert.Matches($"name=\"?{WhisperAsrSpeechToTextClient.AudioFilePartName}\"?", handler.Body);
        Assert.Matches($"filename=\"?{Regex.Escape(expectedFileName)}\"?", handler.Body);
        Assert.Contains("payload", handler.Body);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("en-GB")]
    public async Task GetTextAsync_UsesConfiguredLanguage(string language)
    {
        using var handler = new RecordingHandler(JsonResponse("""{"text":"ok"}"""));
        using var client = CreateSpeechClient(handler, new SpeechToTextConfig { WhisperAsrEndpoint = _endpoint, Language = language });
        using var audio = new MemoryStream("payload"u8.ToArray());

        await client.GetTextAsync(audio, OptionsFor("audio/wav"), TestContext.Current.CancellationToken);

        Assert.Contains($"language={Uri.EscapeDataString(language)}", handler.RequestUri!.Query);
    }

    [Fact]
    public async Task GetTextAsync_TrailingSlashEndpointDoesNotDoublePrefix()
    {
        using var handler = new RecordingHandler(JsonResponse("""{"text":"ok"}"""));
        using var client = CreateSpeechClient(handler, new SpeechToTextConfig { WhisperAsrEndpoint = $"{_endpoint}/" });
        using var audio = new MemoryStream("payload"u8.ToArray());

        await client.GetTextAsync(audio, OptionsFor("audio/wav"), TestContext.Current.CancellationToken);

        Assert.Equal("/asr", handler.RequestUri!.AbsolutePath);
    }

    [Theory]
    [InlineData("""{"text":"hello there"}""", "hello there")]
    [InlineData("""{"text":"  padded  "}""", "  padded  ")]
    [InlineData("""{"text":null}""", "")]
    [InlineData("{}", "")]
    public async Task GetTextAsync_ParsesResponse(string json, string expectedText)
    {
        using var handler = new RecordingHandler(JsonResponse(json));
        using var client = CreateSpeechClient(handler);
        using var audio = new MemoryStream("payload"u8.ToArray());

        var response = await client.GetTextAsync(audio, OptionsFor("audio/wav"), TestContext.Current.CancellationToken);

        Assert.Equal(expectedText, response.Text);
    }

    [Fact]
    public async Task GetTextAsync_DefaultsToWavWhenMediaTypeAbsent()
    {
        using var handler = new RecordingHandler(JsonResponse("""{"text":"ok"}"""));
        using var client = CreateSpeechClient(handler);
        using var audio = new MemoryStream("payload"u8.ToArray());

        await client.GetTextAsync(audio, speechToTextOptions: null, TestContext.Current.CancellationToken);

        Assert.Contains("encode=false", handler.RequestUri!.Query);
        Assert.Matches("filename=\"?audio\\.wav\"?", handler.Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetTextAsync_NonSuccessCarriesStatusCode(HttpStatusCode statusCode)
    {
        using var handler = new RecordingHandler(new HttpResponseMessage(statusCode));
        using var client = CreateSpeechClient(handler);
        using var audio = new MemoryStream("payload"u8.ToArray());

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetTextAsync(audio, OptionsFor("audio/wav"), TestContext.Current.CancellationToken));

        Assert.Equal(statusCode, ex.StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.Unauthorized, false)]
    [InlineData(HttpStatusCode.NotFound, false)]
    [InlineData(HttpStatusCode.RequestTimeout, true)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.BadGateway, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    public void IsTransientStatusCode(HttpStatusCode statusCode, bool expected) =>
        Assert.Equal(expected, WhisperAsrSpeechToTextClient.IsTransientStatusCode(statusCode));

    [Fact]
    public async Task GetTextAsync_PropagatesCancellation()
    {
        using var handler = new BlockingHandler();
        using var client = CreateSpeechClient(handler);
        using var audio = new MemoryStream("payload"u8.ToArray());
        using var cts = new CancellationTokenSource();

        var pending = client.GetTextAsync(audio, OptionsFor("audio/wav"), cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    #region Private helpers

    private static WhisperAsrSpeechToTextClient CreateSpeechClient(HttpMessageHandler handler,
        SpeechToTextConfig? config = null) =>
        new(NullLogger<WhisperAsrSpeechToTextClient>.Instance,
            Options.Create(config ?? new SpeechToTextConfig { WhisperAsrEndpoint = _endpoint }),
            new StubHttpClientFactory(handler));

    private static SpeechToTextOptions OptionsFor(string mediaType) => new()
    {
        AdditionalProperties = new AdditionalPropertiesDictionary
        {
            [WhisperAsrSpeechToTextClient.MediaTypePropertyKey] = mediaType,
        },
    };

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    /// <summary>Captures the outgoing request verbatim and replays a canned response.</summary>
    private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public HttpMethod? Method { get; private set; }

        //Latin1 keeps the multipart preamble readable while preserving arbitrary payload bytes.
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Method = request.Method;
            Body = Encoding.Latin1.GetString(await request.Content!.ReadAsByteArrayAsync(cancellationToken));
            return response;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                response.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    #endregion
}

#pragma warning restore MEAI001
