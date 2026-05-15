using CasCap.HealthChecks;
using Microsoft.Agents.AI;
using StackExchange.Redis;
using System.Collections.Concurrent;

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
public partial class CommunicationsBgService : IBgFeature
{
    private readonly ILogger _logger;
    private readonly SignalCliConfig _signalCliConfig;
    private readonly CommsAgentConfig _commsAgentConfig;
    private readonly AIConfig _aiConfig;
    private readonly INotifier _notifier;
    private readonly AgentCommandHandler _commandHandler;
    private readonly IDatabase _db;
    private readonly IServiceProvider _serviceProvider;
    private readonly SignalCliConnectionHealthCheck _signalCliHealthCheck;
    private readonly IHostEnvironment _env;
    private readonly IPollTracker _pollTracker;
    private readonly CommsDebugNotifier _debugNotifier;
    private readonly IEdgeHardwareQueryService? _edgeHardwareQuerySvc;
    private readonly EdgeHardwareConfig _edgeHardwareConfig;
    private readonly AIAgent? _agent;
    private readonly ProviderConfig? _provider;
    private readonly AgentConfig? _commsAgent;
    private readonly AIAgent? _audioAgent;
    private readonly ProviderConfig? _audioProvider;
    private readonly AgentConfig? _audioAgentConfig;

    private string? _groupId;
    private readonly TaskCompletionSource _groupResolved = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string? _resolvedInstructions;
    private readonly ConcurrentQueue<ReplyRequest> _replyQueue = new();
    private readonly SemaphoreSlim _replySignal = new(0);

    /// <summary>
    /// Initializes a new instance of the <see cref="CommunicationsBgService"/> class.
    /// </summary>
    public CommunicationsBgService(ILogger<CommunicationsBgService> logger,
        IOptions<SignalCliConfig> signalCliConfig,
        IOptions<CommsAgentConfig> commsAgentConfig,
        IOptions<AIConfig> aiConfig,
        IOptions<EdgeHardwareConfig> edgeHardwareConfig,
        IHostEnvironment env,
        CommsDebugNotifier debugNotifier,
        INotifier notifier,
        AgentCommandHandler commandHandler,
        IRemoteCache remoteCache,
        IEventSink<CommsEvent> commsSink,
        IServiceProvider serviceProvider,
        SignalCliConnectionHealthCheck signalCliHealthCheck,
        IPollTracker pollTracker,
        IEdgeHardwareQueryService? edgeHardwareQuerySvc = null)
    {
        _logger = logger;
        _signalCliConfig = signalCliConfig.Value;
        _commsAgentConfig = commsAgentConfig.Value;
        _aiConfig = aiConfig.Value;
        _edgeHardwareConfig = edgeHardwareConfig.Value;
        _env = env;
        _debugNotifier = debugNotifier;
        _notifier = notifier;
        _commandHandler = commandHandler;
        _db = remoteCache.Db;
        _serviceProvider = serviceProvider;
        _signalCliHealthCheck = signalCliHealthCheck;
        _pollTracker = pollTracker;
        _edgeHardwareQuerySvc = edgeHardwareQuerySvc;

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

        if (_aiConfig.Agents.TryGetValue(AgentKeys.AudioAgent, out var audioAgent) && audioAgent.Enabled)
        {
            _audioAgentConfig = audioAgent;
            _audioAgent = serviceProvider.GetKeyedService<AIAgent>(AgentKeys.AudioAgent);
            if (_aiConfig.Providers.TryGetValue(audioAgent.Provider, out var audioProvider))
                _audioProvider = audioProvider;

            if (_audioAgent is not null && _audioProvider is not null)
                _logger.LogInformation("{ClassName} audio agent resolved, model={ModelName}",
                    nameof(CommunicationsBgService), _audioProvider.ModelName);
            else
                _logger.LogWarning("{ClassName} audio agent profile {ProfileKey} not fully configured, audio transcription disabled",
                    nameof(CommunicationsBgService), AgentKeys.AudioAgent);
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

            // Flush pending envelopes so group membership state is current.
            // In JsonRpc (WebSocket) mode ReceiveAsync blocks on a semaphore until a
            // message arrives, so cap the flush with a short timeout to avoid stalling
            // the startup sequence when no envelopes are queued.
            _logger.LogInformation("{ClassName} flushing pending envelopes", nameof(CommunicationsBgService));
            using (var flushCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                flushCts.CancelAfter(TimeSpan.FromMilliseconds(_commsAgentConfig.FlushTimeoutMs));
                try
                {
                    await _notifier.ReceiveAsync(_signalCliConfig.PhoneNumber, flushCts.Token);
                    _logger.LogInformation("{ClassName} pending envelopes flushed", nameof(CommunicationsBgService));
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("{ClassName} flush timed out (no pending envelopes), continuing",
                        nameof(CommunicationsBgService));
                }
            }

            _logger.LogInformation("{ClassName} listing groups for {PhoneNumber}", nameof(CommunicationsBgService), _signalCliConfig.PhoneNumber.MaskPhoneNumber());
            INotificationGroup[]? groups = null;
            try
            {
                groups = await _notifier.ListGroupsAsync(_signalCliConfig.PhoneNumber, cancellationToken);
                _logger.LogInformation("{ClassName} found {GroupCount} group(s): {GroupNames}",
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

            _logger.LogInformation("{ClassName} starting background tasks (notifier={NotifierType})",
                nameof(CommunicationsBgService), _notifier.GetType().Name);

            var replyTask = DrainReplyQueueAsync(cancellationToken);

            _logger.LogInformation("{ClassName} polling for group messages on {PhoneNumber} via {Transport}",
                nameof(CommunicationsBgService), _signalCliConfig.PhoneNumber.MaskPhoneNumber(), _signalCliConfig.TransportMode);
            var incomingTask = PollForMessagesAsync(cancellationToken);

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
