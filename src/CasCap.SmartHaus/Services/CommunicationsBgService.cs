using Microsoft.Agents.AI;
using StackExchange.Redis;

namespace CasCap.Services;

/// <summary>
/// Single-instance background service (<c>Comms</c> feature) that consumes
/// key events from a Redis Stream and incoming notification group messages, feeding both
/// through a configured <see cref="AIAgent"/> for decision-making before relaying responses
/// through the configured Signalizr group with <see cref="ISignalizrClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Stream events:</b> Reads <see cref="CommsEvent"/> entries from the Redis Stream
/// identified by <see cref="CommsAgentConfig.StreamKey"/> using a consumer group.
/// Each event is forwarded to the agent (or sent directly when no agent is configured).
/// </para>
/// <para>
/// <b>Incoming messages:</b> Subscribes to the Signalizr group, routes each message through
/// the agent for processing, and sends the agent's response back to the group.
/// </para>
/// <para>
/// The comms agent is resolved from <see cref="AgentKeys.CommsAgent"/> in
/// <see cref="AIConfig.Agents"/>. When no agent is configured the service forwards
/// stream events as-is and logs received messages without responding.
/// </para>
/// </remarks>
public sealed partial class CommunicationsBgService : IBgFeature
{
    //Every delivery arrives through the gateway, so the identity no longer names the account.
    private const string SignalizrAccount = "signalizr";

    private readonly ILogger _logger;
    private readonly CommsAgentConfig _commsAgentConfig;
    private readonly AIConfig _aiConfig;
    private readonly SpeechToTextConfig _speechToTextConfig;
    private readonly ISignalizrClient _signalizrClient;
    private readonly ISignalMessageDeduplicator _deduplicator;
    private readonly AgentCommandHandler _commandHandler;
    private readonly IRemoteCache _remoteCache;
    private readonly IHostEnvironment _env;
    private readonly IPollTracker _pollTracker;
    private readonly CommsDebugNotifier _debugNotifier;
    private readonly IEdgeHardwareQueryService? _edgeHardwareQuerySvc;
    private readonly EdgeHardwareConfig _edgeHardwareConfig;
    private readonly TimeProvider _timeProvider;
    private readonly AIAgent? _agent;
    private readonly ProviderConfig? _provider;
    private readonly AgentConfig? _commsAgent;
    private readonly IVoiceTranscriptionService _transcriptionSvc;
    private readonly IVoiceSynthesisService _voiceReplySvc;

    private readonly TaskCompletionSource _groupReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string? _resolvedInstructions;
    private readonly Channel<ReplyRequest> _replyChannel;

    private readonly StreamSendThrottle? _streamSendThrottle;
    private long _rateLimitedSinceNotice;
    private long _staleSinceNotice;
    private long _lastDropNoticeTicks;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommunicationsBgService"/> class.
    /// </summary>
    public CommunicationsBgService(ILogger<CommunicationsBgService> logger,
        IOptions<CommsAgentConfig> commsAgentConfig,
        IOptions<AIConfig> aiConfig,
        IOptions<EdgeHardwareConfig> edgeHardwareConfig,
        IOptions<SpeechToTextConfig> speechToTextConfig,
        TimeProvider timeProvider,
        IHostEnvironment env,
        CommsDebugNotifier debugNotifier,
        ISignalizrClient signalizrClient,
        ISignalMessageDeduplicator deduplicator,
        IVoiceTranscriptionService transcriptionSvc,
        IVoiceSynthesisService voiceReplySvc,
        AgentCommandHandler commandHandler,
        IRemoteCache remoteCache,
        IEventSink<CommsEvent> commsSink,
        IServiceProvider serviceProvider,
        IPollTracker pollTracker,
        IEdgeHardwareQueryService? edgeHardwareQuerySvc = null)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _commsAgentConfig = commsAgentConfig.Value;
        _aiConfig = aiConfig.Value;
        _edgeHardwareConfig = edgeHardwareConfig.Value;
        _speechToTextConfig = speechToTextConfig.Value;
        _env = env;
        _debugNotifier = debugNotifier;
        _signalizrClient = signalizrClient;
        _deduplicator = deduplicator;
        _transcriptionSvc = transcriptionSvc;
        _voiceReplySvc = voiceReplySvc;
        _commandHandler = commandHandler;
        //Resolved lazily rather than captured here, so an unreachable cache cannot stop the feature
        //being constructed; only the stream path needs it.
        _remoteCache = remoteCache;
        _pollTracker = pollTracker;
        _edgeHardwareQuerySvc = edgeHardwareQuerySvc;

        // Bound the outbound reply queue so a producer flood cannot grow an unbounded backlog
        // that the gateway drip-feeds for hours. Producers wait for capacity rather than having an
        // already-accepted reply evicted behind their back.
        _replyChannel = Channel.CreateBounded<ReplyRequest>(
            new BoundedChannelOptions(_commsAgentConfig.ReplyQueueCapacity)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait,
            });

        // Token-bucket gate for producer-driven stream events (see ProcessCommsEventAsync).
        if (_commsAgentConfig.StreamSendThrottlingEnabled)
            _streamSendThrottle = new StreamSendThrottle(
                _commsAgentConfig.StreamSendBurst,
                _commsAgentConfig.StreamSendRatePerMinute / 60d,
                _timeProvider);

        var agentProfileName = AgentKeys.CommsAgent;
        if (_aiConfig.Agents.TryGetValue(agentProfileName, out var commsAgent))
        {
            _commsAgent = commsAgent;
            _agent = serviceProvider.GetKeyedService<AIAgent>(agentProfileName);
            if (_aiConfig.Providers.TryGetValue(commsAgent.Provider, out var provider))
                _provider = provider;
            _resolvedInstructions = AgentExtensions.ResolveInstructions(commsAgent,
                typeof(HausServiceCollectionExtensions).Assembly, _aiConfig);
        }

        if (_agent is null || _commsAgent is null || _provider is null)
            LogAgentNotConfigured(_logger, nameof(CommunicationsBgService), agentProfileName);
    }

    /// <inheritdoc/>
    public string FeatureName => FeatureNames.Comms;

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        LogStarting(_logger, nameof(CommunicationsBgService), !string.IsNullOrEmpty(_commsAgentConfig.MonitorGroupName),
            AgentKeys.CommsAgent);
        try
        {
            // Start consuming the comms stream immediately — this must not be gated behind
            // the gateway connection, otherwise stream events (e.g. SecurityAgent findings from
            // MediaBgService) queue indefinitely until Signalizr becomes reachable.
            await EnsureConsumerGroupAsync();
            var streamTask = DrainStreamAsync(cancellationToken);

            await WaitForGroupsAsync(cancellationToken);
            _groupReady.TrySetResult();

            var replyTask = DrainReplyQueueAsync(cancellationToken);

            LogSubscribing(_logger, nameof(CommunicationsBgService));
            var incomingTask = SubscribeToMessagesAsync(cancellationToken);

            //await-await-WhenAny propagates the first faulted task immediately so the
            //service crashes and the pod restarts rather than running in a degraded state.
            await await Task.WhenAny(streamTask, replyTask, incomingTask);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
        {
            LogFatalError(_logger, ex, nameof(CommunicationsBgService));
            throw;
        }
        LogExiting(_logger, nameof(CommunicationsBgService));
    }

    /// <summary>Waits until the Signalizr gateway serves the configured groups.</summary>
    /// <remarks>
    /// An unreachable gateway is retried, because it recovers on its own. A missing chat group
    /// is a configuration fault that retrying cannot fix, so it throws. A missing monitor group
    /// only degrades diagnostics, so it is logged and tolerated.
    /// </remarks>
    private async Task WaitForGroupsAsync(CancellationToken cancellationToken)
    {
        var attempt = 1;
        while (true)
        {
            IReadOnlyList<string> groups;
            try
            {
                groups = await _signalizrClient.GetGroupsAsync(cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                _logger.Log(attempt % 10 == 0 ? LogLevel.Warning : LogLevel.Debug, ex,
                    "{ClassName} Signalizr gateway not reachable, attempt {Attempt}, retrying in {RetryMs}ms",
                    nameof(CommunicationsBgService), attempt, _commsAgentConfig.HealthCheckProbeDelayMs);
                await Task.Delay(_commsAgentConfig.HealthCheckProbeDelayMs, cancellationToken);
                attempt++;
                continue;
            }

            if (!groups.Contains(_commsAgentConfig.GroupName, StringComparer.Ordinal))
                throw new GenericException(
                    "The configured Signalizr chat group is unavailable. Check the exact display name, including spaces and case.");

            if (_commsAgentConfig.MonitorGroupName is { Length: > 0 } monitorGroupName
                && !groups.Contains(monitorGroupName, StringComparer.Ordinal))
                LogMonitorGroupUnavailable(_logger, nameof(CommunicationsBgService));

            LogChatGroupReady(_logger, nameof(CommunicationsBgService));
            return;
        }
    }
}
