using Microsoft.Extensions.Logging.Abstractions;
using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace CasCap.Tests.Unit;

[Trait("Category", "Audio")]
public sealed class DoorBirdAudioCaptureTests
{
    [Fact]
    public async Task CaptureAudio_FramesMuLawBytesAsWave()
    {
        var rawAudio = Enumerable.Range(0, 8_000).Select(value => (byte)value).ToArray();
        using var fixture = CreateFixture(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(rawAudio),
        });

        var result = await fixture.Service.CaptureAudio(
            "bha-api/audio-receive.cgi",
            TimeSpan.FromSeconds(1),
            maximumBytes: 16_000,
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(8_058, result.Length);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(result, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(result, 8, 4));
        Assert.Equal(18, BinaryPrimitives.ReadInt32LittleEndian(result.AsSpan(16, 4)));
        Assert.Equal(7, BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(20, 2)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(22, 2)));
        Assert.Equal(8_000, BinaryPrimitives.ReadInt32LittleEndian(result.AsSpan(24, 4)));
        Assert.Equal("fact", Encoding.ASCII.GetString(result, 38, 4));
        Assert.Equal(8_000, BinaryPrimitives.ReadInt32LittleEndian(result.AsSpan(46, 4)));
        Assert.Equal("data", Encoding.ASCII.GetString(result, 50, 4));
        Assert.Equal(8_000, BinaryPrimitives.ReadInt32LittleEndian(result.AsSpan(54, 4)));
        Assert.Equal(rawAudio, result.AsSpan(58).ToArray());
    }

    [Fact]
    public async Task CaptureAudio_NoContent_ReturnsNull()
    {
        using var fixture = CreateFixture(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        var result = await fixture.Service.CaptureAudio(
            "bha-api/audio-receive.cgi",
            TimeSpan.FromSeconds(1),
            maximumBytes: 16_000,
            TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task CaptureAudio_RequestedBytesExceedLimit_Throws()
    {
        using var fixture = CreateFixture(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Service.CaptureAudio(
            "bha-api/audio-receive.cgi",
            TimeSpan.FromSeconds(2),
            maximumBytes: 8_058,
            TestContext.Current.CancellationToken));
    }

    private static AudioClientFixture CreateFixture(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        var handler = new StubHttpMessageHandler(responseFactory);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://doorbird.example.invalid"),
        };
        var service = new DoorBirdClientService(
            NullLogger<DoorBirdClientService>.Instance,
            new SingleClientFactory(httpClient));
        return new AudioClientFixture(service, httpClient);
    }

    private sealed record AudioClientFixture(
        DoorBirdClientService Service,
        HttpClient HttpClient) : IDisposable
    {
        public void Dispose() => HttpClient.Dispose();
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(responseFactory(request));
    }
}
