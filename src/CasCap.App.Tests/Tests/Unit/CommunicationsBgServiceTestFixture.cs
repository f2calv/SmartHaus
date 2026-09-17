using CasCap.HealthChecks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CasCap.Tests.Unit;

/// <summary>
/// Assembles a <see cref="CommunicationsBgService"/> from deterministic fakes and drives it through
/// its public <see cref="CommunicationsBgService.ExecuteAsync"/> surface.
/// </summary>
/// <remarks>
/// The service exposes no other entry point, so the fixture starts the real execution pipeline —
/// stream consumer, reply drain loop and receive loop — and the tests observe it through
/// <see cref="Notifier"/>.
/// </remarks>
public sealed class CommunicationsBgServiceTestFixture : IAsyncDisposable
{
    /// <summary>The account the service receives on.</summary>
    public const string Account = "+10000000000";

    /// <summary>A group member who sends the envelopes under test.</summary>
    public const string Sender = "+10000000001";

    /// <summary>The display name of the configured group.</summary>
    public const string GroupName = "haus-test";

    /// <summary>The identifier of the configured group.</summary>
    public const string GroupId = "haus-test-group-id";

    /// <summary>An identifier for a group the service must ignore.</summary>
    public const string OtherGroupId = "other-group-id";

    /// <summary>The single generic reply sent when attachment cleanup could not complete.</summary>
    public const string CleanupFailureReply = "\u26A0\uFE0F Sorry, I could not process that message.";

    private readonly CancellationTokenSource _cts = new();
    private Task? _execution;

    /// <summary>The notifier fake feeding envelopes in and recording everything sent out.</summary>
    public FakeNotifier Notifier { get; } = new();

    /// <summary>The attachment cleaner fake.</summary>
    public FakeSignalAttachmentCleaner Cleaner { get; } = new();

    /// <summary>The duplicate-suppression fake.</summary>
    public FakeSignalMessageDeduplicator Deduplicator { get; } = new();

    /// <summary>The comms event sink fake.</summary>
    public FakeEventSink EventSink { get; } = new();

    /// <summary>The poll tracker fake.</summary>
    public FakePollTracker PollTracker { get; } = new();

    /// <summary>The service under test.</summary>
    public CommunicationsBgService Service { get; }

    /// <summary>
    /// Builds the service.
    /// </summary>
    /// <param name="voiceMode">The voice-processing mode under test.</param>
    /// <param name="agentAvailable">Whether a comms agent is resolvable, enabling the reply queue.</param>
    /// <param name="replyQueueCapacity">Bound applied to the outbound reply queue.</param>
    public CommunicationsBgServiceTestFixture(
        VoiceProcessingMode voiceMode = VoiceProcessingMode.Enabled,
        bool agentAvailable = true,
        int replyQueueCapacity = 100)
    {
        Notifier.Groups.Add(new FakeNotificationGroup { Id = GroupId, Name = GroupName, Members = [Account, Sender] });

        var signalCliConfig = Options.Create(new SignalCliConfig
        {
            BaseAddress = "http://localhost:8080",
            PhoneNumber = Account,
            TransportMode = SignalCliTransport.JsonRpc,
        });
        var commsAgentConfig = Options.Create(new CommsAgentConfig
        {
            GroupName = GroupName,
            GroupId = GroupId,
            //Long enough that the idle stream consumer parks instead of spinning for the test's duration.
            PollingIntervalMs = 60_000,
            //Short enough that the startup flush gives up promptly when no envelope is pre-queued.
            FlushTimeoutMs = 50,
            ReplyQueueCapacity = replyQueueCapacity,
            StreamSendThrottlingEnabled = false,
        });
        var aiConfig = Options.Create(BuildAIConfig());
        var edgeHardwareConfig = Options.Create(new EdgeHardwareConfig
        {
            MetricNamePrefix = "haus",
            OtelServiceName = nameof(CasCap),
            AzureTableStorageConnectionString = "UseDevelopmentStorage=true",
            HealthCheckAzureTableStorage = KubernetesProbeTypes.None,
            Sinks = new SinkConfig { AvailableSinks = new Dictionary<string, SinkConfigParams>() },
        });
        var speechToTextConfig = Options.Create(new SpeechToTextConfig { Mode = voiceMode });

        var services = new ServiceCollection();
        if (agentAvailable)
            services.AddKeyedSingleton<AIAgent>(AgentKeys.CommsAgent, new StubAIAgent());
        var serviceProvider = services.BuildServiceProvider();

        var env = new FakeHostEnvironment();
        var debugNotifier = new CommsDebugNotifier(NullLogger<CommsDebugNotifier>.Instance, signalCliConfig,
            edgeHardwareConfig, Notifier, serviceProvider);
        var commandHandler = new AgentCommandHandler(NullLogger<AgentCommandHandler>.Instance, aiConfig,
            new FakeSessionStore());
        var healthCheck = new SignalCliConnectionHealthCheck(NullLogger<SignalCliConnectionHealthCheck>.Instance,
            signalCliConfig, env, new StubHttpClientFactory());

        Service = new CommunicationsBgService(
            NullLogger<CommunicationsBgService>.Instance,
            signalCliConfig,
            commsAgentConfig,
            aiConfig,
            edgeHardwareConfig,
            speechToTextConfig,
            TimeProvider.System,
            env,
            debugNotifier,
            Notifier,
            new FakeSignalCliClient(),
            Cleaner,
            Deduplicator,
            commandHandler,
            new StubRemoteCache(),
            EventSink,
            serviceProvider,
            healthCheck,
            PollTracker);
    }

    /// <summary>
    /// Starts execution and waits until the group has been resolved and the receive loop is polling,
    /// so envelopes queued afterwards travel the normal inbound path.
    /// </summary>
    public async Task StartAsync()
    {
        _execution = Service.ExecuteAsync(_cts.Token);
        await WaitForAsync(() => Notifier.ListGroupsCallCount > 0 && Notifier.ReceiveCallCount > 1);
    }

    /// <summary>Builds an ordinary text envelope from the configured group.</summary>
    public static FakeReceivedNotification TextEnvelope(string message, long timestamp,
        string sender = Sender, string? groupId = GroupId) =>
        new()
        {
            Sender = sender,
            GroupId = groupId,
            Message = message,
            Timestamp = timestamp,
        };

    /// <summary>Builds an attachment-only envelope from the configured group.</summary>
    public static FakeReceivedNotification AttachmentEnvelope(long timestamp, params (string Id, string ContentType)[] attachments) =>
        new()
        {
            Sender = Sender,
            GroupId = GroupId,
            Timestamp = timestamp,
            Attachments = [.. attachments.Select(a => new FakeNotificationAttachment { Id = a.Id, ContentType = a.ContentType })],
        };

    /// <summary>Polls <paramref name="condition"/> until it holds or the timeout elapses.</summary>
    public static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 10_000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (!condition())
        {
            if (Environment.TickCount64 > deadline)
                Assert.Fail($"condition not met within {timeoutMs}ms");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Asserts <paramref name="condition"/> stays false for <paramref name="settleMs"/>, proving an
    /// absence rather than merely observing one early.
    /// </summary>
    public static async Task AssertStaysFalseAsync(Func<bool> condition, int settleMs = 500)
    {
        var deadline = Environment.TickCount64 + settleMs;
        while (Environment.TickCount64 < deadline)
        {
            Assert.False(condition());
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        //Release any gate first, or a held drain loop would never observe the cancellation.
        Notifier.ReleaseStartProcessing();
        await _cts.CancelAsync();
        if (_execution is not null)
        {
            try
            {
                await _execution.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
            {
                //Cancellation is the expected shutdown path.
            }
        }
        _cts.Dispose();
    }

    private static AIConfig BuildAIConfig() => new()
    {
        Providers = new Dictionary<string, ProviderConfig>
        {
            ["Stub"] = new() { Type = AgentType.Ollama, ModelName = "stub-model" },
        },
        Agents = new Dictionary<string, AgentConfig>
        {
            [AgentKeys.CommsAgent] = new()
            {
                Provider = "Stub",
                Name = AgentKeys.CommsAgent,
                Description = "comms agent under test",
                Prompt = "respond",
                Instructions = "you are a test double",
            },
        },
    };
}
