using CasCap.Abstractions;
using CasCap.Common.Models;
using CasCap.HealthChecks;
using CasCap.Models.Dtos;
using Knx.Falcon;
using Knx.Falcon.KnxnetIp;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using System.Runtime.CompilerServices;

namespace CasCap.Tests;

/// <summary>Verifies ownership, callback publishing and shutdown without Redis or a KNX network.</summary>
[Trait("Category", "Leadership")]
public sealed class KnxLeadershipTests
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData(ServiceFamily.Tunneling, ShardingMode.Unified)]
    [InlineData(ServiceFamily.Routing, ShardingMode.Partitioned)]
    public async Task ExecuteAsync_DelegatesExistingResourceWithoutStartingStandbyWork(ServiceFamily family, ShardingMode sharding)
    {
        using var fixture = new Fixture(family, sharding);
        var runner = new CapturingRunner();
        var created = 0;
        var sut = fixture.CreateWorker(runner, () =>
        {
            created++;
            return new TestMonitor();
        });

        await sut.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Knx", sut.FeatureName);
        Assert.Equal("knx:monitor", runner.Resource);
        Assert.Equal(TestContext.Current.CancellationToken, runner.HostToken);
        Assert.NotNull(runner.Work);
        Assert.Equal(0, created);
        Assert.False(fixture.Outgoing.Started.Task.IsCompleted);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsUnsupportedTunnelingShardingBeforeElection()
    {
        using var fixture = new Fixture(ServiceFamily.Tunneling, ShardingMode.Partitioned);
        var runner = new CapturingRunner();
        var sut = fixture.CreateWorker(runner, () => new TestMonitor());

        await Assert.ThrowsAsync<NotSupportedException>(() => sut.ExecuteAsync(TestContext.Current.CancellationToken));

        Assert.Null(runner.Work);
    }

    [Theory]
    [InlineData("connect")]
    [InlineData("run")]
    [InlineData("complete")]
    public async Task Ownership_FailureDisposesSessionAndCancelsSender(string failure)
    {
        using var fixture = new Fixture();
        fixture.Health.ConnectionActive = true;
        fixture.Outgoing.AllowCleanup.SetResult();
        var monitor = new TestMonitor { Failure = failure };
        monitor.AllowDispose.SetResult();
        var runner = new CapturingRunner();
        await fixture.CreateWorker(runner, () => monitor).ExecuteAsync(TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.Work!(TestContext.Current.CancellationToken).WaitAsync(_timeout, TestContext.Current.CancellationToken));

        Assert.NotEmpty(exception.Message);
        Assert.True(monitor.Disposed);
        if (fixture.Outgoing.Started.Task.IsCompleted)
            Assert.True(fixture.Outgoing.Token.IsCancellationRequested);
        if (failure == "connect")
            Assert.False(fixture.Outgoing.Started.Task.IsCompleted);
    }

    [Fact]
    public async Task Ownership_JoinsSenderBeforeDisposalAndCreatesFreshSessions()
    {
        using var fixture = new Fixture();
        fixture.Health.ConnectionActive = true;
        var sessions = new List<TestMonitor>();
        var runner = new CapturingRunner();
        await fixture.CreateWorker(runner, () =>
        {
            var monitor = new TestMonitor();
            sessions.Add(monitor);
            return monitor;
        }).ExecuteAsync(TestContext.Current.CancellationToken);

        using var firstOwnership = new CancellationTokenSource();
        var first = runner.Work!(firstOwnership.Token);
        await fixture.Outgoing.Started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        var firstSession = Assert.Single(sessions);
        Assert.Equal(firstOwnership.Token, firstSession.ConnectToken);

        await firstOwnership.CancelAsync();
        await fixture.Outgoing.Cleaning.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        Assert.False(first.IsCompleted);
        Assert.False(firstSession.Disposing.Task.IsCompleted);
        Assert.True(fixture.Outgoing.Token.IsCancellationRequested);

        fixture.Outgoing.AllowCleanup.SetResult();
        await firstSession.Disposing.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        Assert.False(first.IsCompleted);
        firstSession.AllowDispose.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            first.WaitAsync(_timeout, TestContext.Current.CancellationToken));
        Assert.True(firstSession.Disposed);

        using var secondOwnership = new CancellationTokenSource();
        var second = runner.Work(secondOwnership.Token);
        Assert.Equal(2, sessions.Count);
        Assert.NotSame(firstSession, sessions[1]);
        Assert.Equal(secondOwnership.Token, sessions[1].ConnectToken);
        sessions[1].AllowDispose.SetResult();
        await secondOwnership.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            second.WaitAsync(_timeout, TestContext.Current.CancellationToken));
        Assert.True(sessions[1].Disposed);
    }

    [Theory]
    [InlineData(ServiceFamily.Tunneling)]
    [InlineData(ServiceFamily.Routing)]
    public async Task ConnectAsync_CancelledOwnershipDoesNotStartDiscovery(ServiceFamily family)
    {
        using var fixture = new Fixture(family);
        await using var monitor = fixture.CreateMonitor(new IncomingBroker());
        using var ownership = new CancellationTokenSource();
        await ownership.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => monitor.ConnectAsync(ownership.Token));

        Assert.False(fixture.Health.IsLeader);
        Assert.False(fixture.Health.ConnectionActive);
    }

    [Fact]
    public async Task RunAsync_JoinsPublicationAndRejectsLateTelegrams()
    {
        using var fixture = new Fixture();
        var broker = new IncomingBroker();
        await using var monitor = fixture.CreateMonitor(broker);
        using var ownership = new CancellationTokenSource();
        var telegram = CreateTelegram();
        Assert.True(monitor.TryQueueTelegram(telegram));
        var running = monitor.RunAsync(ownership.Token);
        await broker.Started.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
        fixture.Health.IsLeader = true;

        await ownership.CancelAsync();
        await broker.Cleaning.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);

        Assert.False(fixture.Health.IsLeader);
        Assert.False(fixture.Health.ConnectionActive);
        Assert.False(monitor.TryQueueTelegram(telegram));
        Assert.False(running.IsCompleted);
        Assert.True(broker.Token.IsCancellationRequested);
        broker.AllowCleanup.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            running.WaitAsync(_timeout, TestContext.Current.CancellationToken));
        Assert.Same(telegram, broker.Telegram);
    }

    [Fact]
    public async Task RunAsync_PublicationFaultTerminatesSiblingReaders()
    {
        using var fixture = new Fixture();
        var broker = new IncomingBroker { Fail = true };
        await using var monitor = fixture.CreateMonitor(broker);
        Assert.True(monitor.TryQueueTelegram(CreateTelegram()));
        fixture.Health.IsLeader = true;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            monitor.RunAsync(TestContext.Current.CancellationToken).WaitAsync(_timeout, TestContext.Current.CancellationToken));

        Assert.Equal("Publication failed.", exception.Message);
        Assert.False(fixture.Health.IsLeader);
        Assert.False(fixture.Health.ConnectionActive);
        Assert.False(monitor.TryQueueTelegram(CreateTelegram()));
    }

    [Fact]
    public async Task Sender_WaitsForConnectivityWithoutConsumingCommands()
    {
        using var fixture = new Fixture();
        using var ownership = new CancellationTokenSource();
        var running = fixture.CreateSender().ExecuteAsync(ownership.Token);

        Assert.False(fixture.Outgoing.Started.Task.IsCompleted);
        Assert.False(running.IsCompleted);
        await ownership.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            running.WaitAsync(_timeout, TestContext.Current.CancellationToken));
        Assert.False(fixture.Outgoing.Started.Task.IsCompleted);
    }

    private static KnxEvent CreateTelegram()
        => new(DateTime.UnixEpoch, new KnxGroupEvent(),
            new KnxGroupAddressParsed(new KnxGroupAddressXml { Name = "EG-LI-Kitchen-SW", Address = "0/0/1", DPTs = "DPST-1-1" }),
            new GroupValue(true), true, null);

    private static TaskCompletionSource Gate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class Fixture : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        public KnxConnectionHealthCheck Health { get; } = new();
        public OutgoingBroker Outgoing { get; } = new();
        public IOptions<KnxConfig> Config { get; }
        public KnxGroupAddressLookupService Lookup { get; }

        public Fixture(ServiceFamily family = ServiceFamily.Tunneling, ShardingMode sharding = ShardingMode.Unified)
        {
            File.WriteAllText(_path, """
                <GroupAddress-Export xmlns="http://knx.org/xml/ga-export/01">
                  <GroupRange Name="Example" RangeStart="0" RangeEnd="2047">
                    <GroupRange Name="Lighting" RangeStart="0" RangeEnd="255">
                      <GroupAddress Name="EG-LI-Kitchen-SW" Address="0/0/1" DPTs="DPST-1-1" />
                    </GroupRange>
                  </GroupRange>
                </GroupAddress-Export>
                """);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                [nameof(KnxConfig.ServiceFamily)] = family.ToString(),
                [nameof(KnxConfig.ShardingMode)] = sharding.ToString(),
                [nameof(KnxConfig.GroupAddressXmlFilePath)] = _path
            }).Build();
            Config = Options.Create(configuration.Get<KnxConfig>()!);
            Lookup = new KnxGroupAddressLookupService(NullLogger<KnxGroupAddressLookupService>.Instance,
                Config, new TestEnvironment(), new KnxGroupAddressLookupHealthCheck(), TimeProvider.System);
        }

        public KnxMonitorBgService CreateWorker(ILeadershipRunner runner, Func<IKnxMonitor> factory)
            => new(NullLogger<KnxMonitorBgService>.Instance, Config, Lookup, runner, factory, CreateSender());

        public KnxSenderBgService CreateSender()
            => new(NullLogger<KnxSenderBgService>.Instance, Config, TimeProvider.System, Health, Outgoing);

        public KnxMonitorService CreateMonitor(IKnxTelegramBroker<KnxEvent> incoming)
            => new(NullLogger<KnxMonitorService>.Instance, Config, TimeProvider.System, Lookup, Health, incoming, new KnxConnectionNotifier());

        public void Dispose() => File.Delete(_path);
    }

    private sealed class CapturingRunner : ILeadershipRunner
    {
        public string? Resource { get; private set; }
        public CancellationToken HostToken { get; private set; }
        public Func<CancellationToken, Task>? Work { get; private set; }

        public Task RunAsync(string resource, Func<CancellationToken, Task> runAsync, CancellationToken cancellationToken)
        {
            Resource = resource;
            HostToken = cancellationToken;
            Work = runAsync;
            return Task.CompletedTask;
        }
    }

    private sealed class TestMonitor : IKnxMonitor
    {
        public string? Failure { get; init; }
        public CancellationToken ConnectToken { get; private set; }
        public TaskCompletionSource Disposing { get; } = Gate();
        public TaskCompletionSource AllowDispose { get; } = Gate();
        public bool Disposed { get; private set; }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            ConnectToken = cancellationToken;
            return Failure == "connect"
                ? Task.FromException(new InvalidOperationException("Connection failed."))
                : Task.CompletedTask;
        }

        public Task RunAsync(CancellationToken cancellationToken)
            => Failure switch
            {
                "run" => Task.FromException(new InvalidOperationException("Monitor failed.")),
                "complete" => Task.CompletedTask,
                _ => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
            };

        public async ValueTask DisposeAsync()
        {
            Disposing.TrySetResult();
            await AllowDispose.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
            Disposed = true;
        }
    }

    private sealed class OutgoingBroker : IKnxTelegramBroker<KnxOutgoingTelegram>
    {
        public TaskCompletionSource Started { get; } = Gate();
        public TaskCompletionSource Cleaning { get; } = Gate();
        public TaskCompletionSource AllowCleanup { get; } = Gate();
        public CancellationToken Token { get; private set; }

        public ValueTask PublishAsync(KnxOutgoingTelegram item, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<KnxOutgoingTelegram> SubscribeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Token = cancellationToken;
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                yield break;
            }
            finally
            {
                Cleaning.TrySetResult();
                await AllowCleanup.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
            }
        }
    }

    private sealed class IncomingBroker : IKnxTelegramBroker<KnxEvent>
    {
        public bool Fail { get; init; }
        public KnxEvent? Telegram { get; private set; }
        public CancellationToken Token { get; private set; }
        public TaskCompletionSource Started { get; } = Gate();
        public TaskCompletionSource Cleaning { get; } = Gate();
        public TaskCompletionSource AllowCleanup { get; } = Gate();

        public async ValueTask PublishAsync(KnxEvent item, CancellationToken cancellationToken = default)
        {
            Telegram = item;
            Token = cancellationToken;
            Started.SetResult();
            if (Fail)
                throw new InvalidOperationException("Publication failed.");
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            finally
            {
                Cleaning.SetResult();
                await AllowCleanup.Task.WaitAsync(_timeout, TestContext.Current.CancellationToken);
            }
        }

        public IAsyncEnumerable<KnxEvent> SubscribeAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = nameof(KnxLeadershipTests);
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
