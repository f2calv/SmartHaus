using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Http;

namespace CasCap.Tests.Unit;

/// <summary>Verifies DDNS leadership wiring and cancellation without DNS, Azure or Redis access.</summary>
[Trait("Category", "Leadership")]
public sealed class DDnsLeadershipTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ExecuteAsync_DelegatesTheExistingResourceAndWorker()
    {
        using var handler = new BlockingHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
        var worker = CreateWorker(client);
        var runner = new TestRunner();
        var sut = new DDnsBgService(NullLogger<DDnsBgService>.Instance, runner, worker);

        await sut.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DDnsConfig.FeatureName, sut.FeatureName);
        Assert.Equal("ddns", runner.Resource);
        Assert.Equal(TestContext.Current.CancellationToken, runner.HostToken);
        Assert.Same(worker, runner.Work!.Target);
        Assert.Equal(nameof(DDnsUpdateService.RunAsync), runner.Work.Method.Name);
        Assert.False(handler.Started.Task.IsCompleted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_ReachesIpDiscoveryAndPropagates(bool runWorker)
    {
        using var handler = new BlockingHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.com/") };
        using var stopping = new CancellationTokenSource();
        var running = runWorker
            ? CreateWorker(client).RunAsync(stopping.Token)
            : CreateFinder(client).GetIp(stopping.Token);
        await handler.Started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        await stopping.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            running.WaitAsync(_timeout, TestContext.Current.CancellationToken));
        Assert.True(handler.Token.IsCancellationRequested);
    }

    private static DDnsUpdateService CreateWorker(HttpClient client)
        => new(NullLogger<DDnsUpdateService>.Instance,
            Options.Create(new AzureAuthConfig { KeyVaultName = "example" }),
            Options.Create(new DDnsConfig
            {
                BaseAddress = "https://example.com/",
                DnsResourceGroupName = "example",
                DnsZoneName = "example.com",
                DnsMetaDataKey = "example",
                RefreshDelayMs = 30_000
            }),
            TimeProvider.System, CreateFinder(client), new TestEnvironment(), new TestSink());

    private static DDnsFindMyIpClientService CreateFinder(HttpClient client)
        => new(NullLogger<DDnsFindMyIpClientService>.Instance, new TestClientFactory(client));

    private sealed class TestRunner : ILeadershipRunner
    {
        public string? Resource { get; private set; }
        public Func<CancellationToken, Task>? Work { get; private set; }
        public CancellationToken HostToken { get; private set; }

        public Task RunAsync(string resource, Func<CancellationToken, Task> runAsync, CancellationToken cancellationToken)
        {
            Resource = resource;
            Work = runAsync;
            HostToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken Token { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            Started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The request must be cancelled.");
        }
    }

    private sealed class TestClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Example";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestSink : IEventSink<CommsEvent>
    {
        public string SinkType => "Test";

        public Task WriteEvent(CommsEvent @event, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Cancellation tests must not reach DNS notifications.");
    }
}
