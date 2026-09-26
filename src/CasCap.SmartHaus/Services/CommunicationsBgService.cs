using CasCap.HealthChecks;
using Microsoft.Agents.AI;
using StackExchange.Redis;

namespace CasCap.Services;

/// <summary>
/// Single-instance background service (<c>Comms</c> feature) that consumes
/// key events from a Redis Stream and incoming notification group messages, feeding both
/// through a configured <see cref="AIAgent"/> for decision-making before relaying responses
/// via <see cref="INotifier"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Stream events:</b> Reads <see cref="CommsEvent"/> entries from the Redis Stream
/// identified by <see cref="CommsAgentConfig.StreamKey"/> using a consumer group.
/// Each event is forwarded to the agent (or sent directly when no agent is configured).
/// </para>
/// <para>
/// <b>Incoming messages:</b> Polls the notification API for new messages, routes data
/// messages through the agent for processing, and sends the agent's response back to
/// the group.
/// </para>
/// <para>
/// The comms agent is resolved from <see cref="AgentKeys.CommsAgent"/> in
/// <see cref="AIConfig.Agents"/>. When no agent is configured the service forwards
/// stream events as-is and logs received messages without responding.
/// </para>
/// </remarks>
public sealed partial class CommunicationsBgService : IBgFeature
{
    private readonly ILogger _logger;
    private readonly SignalCliConfig _signalCliConfig;
    private readonly CommsAgentConfig _commsAgentConfig;
    private readonly AIConfig _aiConfig;
    private readonly SpeechToTextConfig _speechToTextConfig;
    private readonly INotifier _notifier;
    private readonly ISignalizrClient _signalizrClient;
    private readonly ISignalAttachmentCleaner _attachmentCleaner;
    private readonly ISignalMessageDeduplicator _deduplicator;
    private readonly AgentCommandHandler _commandHandler;
    private readonly IRemoteCache _remoteCache;
    private readonly SignalCliConnectionHealthCheck _signalCliHealthCheck;
    private readonly IHostEnvironment _env;
    private readonly IPollTracker _pollTracker;
    private readonly CommsDebugNotifier _debugNotifier;
    private readonly IEdgeHardwareQueryService? _edgeHardwareQuerySvc;
    private readonly EdgeHardwareConfig _edgeHardwareConfig;
    private readonly TimeProvider _timeProvider;
    private readonly AIAgent? _agent;
    private readonly ProviderConfig? _provider;
    private readonly AgentConfig? _commsAgent;
    private readonly VoiceMessageTranscriptionService _transcriptionSvc;
    private readonly VoiceReplySynthesisService _voiceReplySvc;

    private string? _groupId;
    private readonly TaskCompletionSource _groupResolved = new(TaskCreationOptions.RunContinuationsAsynchronously);
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
        IOptions<SignalCliConfig> signalCliConfig,
        IOptions<CommsAgentConfig> commsAgentConfig,
        IOptions<AIConfig> aiConfig,
        IOptions<EdgeHardwareConfig> edgeHardwareConfig,
        IOptions<SpeechToTextConfig> speechToTextConfig,
        TimeProvider timeProvider,
        IHostEnvironment env,
        CommsDebugNotifier debugNotifier,
        INotifier notifier,
        ISignalizrClient signalizrClient,
        ISignalAttachmentCleaner attachmentCleaner,
        ISignalMessageDeduplicator deduplicator,
        VoiceMessageTranscriptionService transcriptionSvc,
        VoiceReplySynthesisService voiceReplySvc,
        AgentCommandHandler commandHandler,
        IRemoteCache remoteCache,
        IEventSink<CommsEvent> commsSink,
        IServiceProvider serviceProvider,
        SignalCliConnectionHealthCheck signalCliHealthCheck,
        IPollTracker pollTracker,
        IEdgeHardwareQueryService? edgeHardwareQuerySvc = null)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _signalCliConfig = signalCliConfig.Value;
        _commsAgentConfig = commsAgentConfig.Value;
        _aiConfig = aiConfig.Value;
        _edgeHardwareConfig = edgeHardwareConfig.Value;
        _speechToTextConfig = speechToTextConfig.Value;
        _env = env;
        _debugNotifier = debugNotifier;
        _notifier = notifier;
        _signalizrClient = signalizrClient;
        _attachmentCleaner = attachmentCleaner;
        _deduplicator = deduplicator;
        _transcriptionSvc = transcriptionSvc;
        _voiceReplySvc = voiceReplySvc;
        _commandHandler = commandHandler;
        //Resolved lazily rather than captured here, so an unreachable cache cannot stop the feature
        //being constructed; only the stream path needs it.
        _remoteCache = remoteCache;
        _signalCliHealthCheck = signalCliHealthCheck;
        _pollTracker = pollTracker;
        _edgeHardwareQuerySvc = edgeHardwareQuerySvc;

        // Bound the outbound reply queue so a producer flood cannot grow an unbounded backlog
        // that signal-cli drip-feeds for hours. Producers wait for capacity rather than having an
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
            _logger.LogWarning("{ClassName} agent profile {ProfileKey} not fully configured, agent responses disabled",
                nameof(CommunicationsBgService), agentProfileName);
    }

    /// <inheritdoc/>
    public string FeatureName => FeatureNames.Comms;

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{ClassName} starting, transport={Transport}, phoneNumber={PhoneNumber}, phoneNumberDebug={PhoneNumberDebug}, groupName={GroupName}, agentProfile={AgentProfile}",
            nameof(CommunicationsBgService), _signalCliConfig.TransportMode, _signalCliConfig.PhoneNumber.MaskPhoneNumber(),
            _signalCliConfig.PhoneNumberDebug?.MaskPhoneNumber() ?? "(disabled)", _commsAgentConfig.GroupName, AgentKeys.CommsAgent);
        try
        {
            // Start consuming the comms stream immediately — this must not be gated behind
            // the Signal messenger connection, otherwise stream events (e.g. SecurityAgent
            // findings from MediaBgService) queue indefinitely until signal-cli becomes reachable.
            await EnsureConsumerGroupAsync();
            var streamTask = DrainStreamAsync(cancellationToken);

            // Wait for signal-cli REST API to be reachable before attempting WebSocket connection.
            if (!_env.IsDevelopment())
            {
                var attempt = 1;
                while (!_signalCliHealthCheck.ConnectionActive && !cancellationToken.IsCancellationRequested)
                {
                    _logger.Log(attempt % 10 == 0 ? LogLevel.Warning : LogLevel.Debug,
                        "{ClassName} signal-cli readiness probe not yet healthy, attempt {Attempt}, retrying in {RetryMs}ms",
                        nameof(CommunicationsBgService), attempt, _commsAgentConfig.HealthCheckProbeDelayMs);
                    await Task.Delay(_commsAgentConfig.HealthCheckProbeDelayMs, cancellationToken);
                    attempt++;
                }
                _logger.LogInformation("{ClassName} signal-cli readiness probe healthy", nameof(CommunicationsBgService));
            }

            // Update the Signal profile display name to include the active model.
            await UpdateSignalProfileNameAsync(_provider?.ModelName);

            var channels = await _signalizrClient.GetChannelsAsync(cancellationToken);
            if (!channels.Contains(_commsAgentConfig.ChannelName, StringComparer.Ordinal))
                throw new GenericException(
                    $"Signalizr channel '{_commsAgentConfig.ChannelName}' is not configured.");
            _logger.LogDebug("{ClassName} Signalizr channel {ChannelName} is ready",
                nameof(CommunicationsBgService), _commsAgentConfig.ChannelName);

            _logger.LogDebug("{ClassName} listing groups for {PhoneNumber}", nameof(CommunicationsBgService), _signalCliConfig.PhoneNumber.MaskPhoneNumber());
            INotificationGroup[]? groups = null;
            try
            {
                groups = await _notifier.ListGroupsAsync(_signalCliConfig.PhoneNumber, cancellationToken);
                _logger.LogDebug("{ClassName} found {GroupCount} group(s): {GroupNames}",
                    nameof(CommunicationsBgService), groups?.Length ?? 0,
                    groups is not null ? string.Join(", ", groups.Select(g => g.Name)) : "(none)");
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
            {
                _logger.LogWarning(ex, "{ClassName} ListGroups failed ({ExceptionType}: {ExceptionMessage})",
                    nameof(CommunicationsBgService), ex.GetType().Name, ex.Message);
            }

            var group = groups?.FirstOrDefault(g => g.Name == _commsAgentConfig.GroupName);
            if (group is not null)
            {
                _groupId = group.Id;
                _logger.LogInformation("{ClassName} resolved group {GroupName} to {GroupId} ({MemberCount} members)",
                    nameof(CommunicationsBgService), group.Name, _groupId, group.Members.Length);
            }
            else if (!string.IsNullOrWhiteSpace(_commsAgentConfig.GroupId))
            {
                _groupId = _commsAgentConfig.GroupId;
                _logger.LogWarning("{ClassName} group {GroupName} not found via ListGroups, falling back to configured GroupId={GroupId}",
                    nameof(CommunicationsBgService), _commsAgentConfig.GroupName, _groupId);
            }
            else
            {
                throw new GenericException(
                    $"group '{_commsAgentConfig.GroupName}' not found among [{(groups is not null ? string.Join(", ", groups.Select(g => g.Name)) : "(none)")}] and no GroupId fallback configured");
            }

            _groupResolved.TrySetResult();

            _logger.LogInformation("{ClassName} starting background tasks (transport={TransportType})",
                nameof(CommunicationsBgService), _signalizrClient.GetType().Name);

            var replyTask = DrainReplyQueueAsync(cancellationToken);

            _logger.LogInformation("{ClassName} subscribing to Signalizr channel {ChannelName}",
                nameof(CommunicationsBgService), _commsAgentConfig.ChannelName);
            var incomingTask = SubscribeToMessagesAsync(cancellationToken);

            //await-await-WhenAny propagates the first faulted task immediately so the
            //service crashes and the pod restarts rather than running in a degraded state.
            await await Task.WhenAny(streamTask, replyTask, incomingTask);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
        {
            _logger.LogError(ex, "{ClassName} fatal error during execution", nameof(CommunicationsBgService));
            throw;
        }
        _logger.LogInformation("{ClassName} exiting", nameof(CommunicationsBgService));
    }

}
