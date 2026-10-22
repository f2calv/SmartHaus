using CasCap.Abstractions;
using CasCap.Common.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace CasCap.Tests;

/// <summary>Checks datapoint validation, normalization and read cancellation without a device.</summary>
[Trait("Category", "Device Commands")]
public sealed class BuderusQueryServiceTests
{
    [Theory]
    [InlineData("example/value")]
    [InlineData("/example/value")]
    public async Task RejectedWrite_ReportsNormalizedPath(string id)
    {
        using var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var service = CreateService(client);

        var result = await service.SetDataPoint(id, "50", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("Failed to write datapoint '/example/value'. The datapoint may not exist or may not be writeable.", result.Error);
        Assert.Equal("/example/value", handler.RequestUri?.AbsolutePath);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task BlankPath_IsRejectedBeforeDeviceAccess(string id)
    {
        using var handler = new RecordingHandler();
        using var client = new HttpClient(handler);

        var result = await CreateService(client).SetDataPoint(id, "50", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("Datapoint id must not be empty.", result.Error);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task CancelledPreflightRead_PropagatesWithoutWriting()
    {
        using var handler = new RecordingHandler { Block = true };
        using var client = new HttpClient(handler);
        using var stopping = new CancellationTokenSource();
        var task = CreateService(client).SetDataPoint("example/value", "50", stopping.Token);
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        await stopping.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Equal(1, handler.Calls);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.True(handler.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task McpWrite_PreservesBooleanFailureContract()
    {
        using var handler = new RecordingHandler();
        using var client = new HttpClient(handler);
        var tool = new HeatPumpMcpQueryService(CreateService(client));

        var accepted = await tool.SetHeatPumpDataPoint("example/value", "50", TestContext.Current.CancellationToken);

        Assert.False(accepted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiscoveryCancellation_Propagates(bool alreadyCancelled)
    {
        using var handler = new RecordingHandler { Block = true };
        using var client = new HttpClient(handler);
        using var stopping = new CancellationTokenSource();
        if (alreadyCancelled)
            await stopping.CancelAsync();

        var task = CreateDevice(client).GetAllDataPoints(stopping.Token);
        if (!alreadyCancelled)
        {
            await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await stopping.CancelAsync();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Equal(alreadyCancelled ? 0 : 1, handler.Calls);
    }

    private static BuderusQueryService CreateService(HttpClient client)
        => new(NullLogger<BuderusQueryService>.Instance, CreateDevice(client), new UnusedQuery());

    private static BuderusKm200ClientService CreateDevice(HttpClient client)
    {
        var options = Options.Create(new BuderusConfig
        {
            BaseAddress = "http://example.com",
            HealthCheckUri = "system",
            Port = 80,
            GatewayPassword = "example",
            PrivatePassword = "example",
            AzureTableStorageConnectionString = string.Empty,
            HealthCheck = KubernetesProbeTypes.None,
            HealthCheckAzureTableStorage = KubernetesProbeTypes.None,
            Sinks = new SinkConfig { AvailableSinks = [] },
            ConnectionPollingDelayMs = 1000,
            ConnectionLogEscalationInterval = 10,
            MetricNamePrefix = "example",
            OtelServiceName = "example",
        });
        return new BuderusKm200ClientService(NullLogger<BuderusKm200ClientService>.Instance, options, new TestClientFactory(client));
    }

    private sealed class TestClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class UnusedQuery : IBuderusQuery
    {
        public Task<BuderusSnapshot> GetSnapshot() => throw new NotSupportedException();
        public IAsyncEnumerable<BuderusEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public bool Block { get; init; }
        public int Calls { get; private set; }
        public Uri? RequestUri { get; private set; }
        public HttpMethod? Method { get; private set; }
        public CancellationToken Token { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            RequestUri = request.RequestUri;
            Method = request.Method;
            Token = cancellationToken;
            Started.TrySetResult();
            if (Block)
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("Sorry, the requested file does not exist on this server."),
            };
        }
    }
}
