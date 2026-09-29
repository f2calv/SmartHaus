namespace CasCap.Services;

/// <summary>
/// Single-instance background service (<c>Comms</c> feature) that relays key events from a Redis
/// Stream and inbound Signalizr group messages, optionally through an <see cref="ICommsResponder"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Stream events:</b> Reads <see cref="CommsEvent"/> entries from the Redis Stream identified by
/// <see cref="CommsConfig.StreamKey"/> using a consumer group. Each event is routed by
/// <see cref="ICommsGroupRouter"/>; events for the chat group become responder turns when a responder
/// is available, and everything else is formatted by <see cref="ICommsEventFormatter"/> and sent directly.
/// </para>
/// <para>
/// <b>Incoming messages:</b> Subscribes to the Signalizr group. With an available responder, each
/// message is deduplicated, acknowledged, transcribed when it is a voice note, and answered through
/// the bounded reply queue. Without one, messages are logged and otherwise ignored.
/// </para>
/// </remarks>
public sealed partial class CommunicationsBgService(
    ILogger<CommunicationsBgService> logger,
    IOptions<CommsConfig> commsConfig,
    IOptions<SpeechToTextConfig> speechToTextConfig,
    TimeProvider timeProvider,
    IHostEnvironment env,
    ISignalizrClient signalizrClient,
    ISignalMessageDeduplicator deduplicator,
    IVoiceTranscriptionService transcriptionSvc,
    IVoiceSynthesisService voiceReplySvc,
    ICommsEventFormatter eventFormatter,
    ICommsGroupRouter groupRouter,
    IRemoteCache remoteCache,
    ICommsResponder? responder = null) : IBgFeature
{
    //Every delivery arrives through the gateway, so the identity no longer names the account.
    private const string SignalizrAccount = "signalizr";

    private readonly TaskCompletionSource _groupReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Bound the reply queue so a producer flood cannot grow an unbounded backlog that the gateway
    // drip-feeds for hours. Producers wait for capacity rather than having an already-accepted turn
    // evicted behind their back.
    private readonly Channel<CommsTurn> _replyChannel = Channel.CreateBounded<CommsTurn>(
        new BoundedChannelOptions(commsConfig.Value.ReplyQueueCapacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

    // Token-bucket gate for producer-driven stream events (see ProcessCommsEventAsync).
    private readonly StreamSendThrottle? _streamSendThrottle = commsConfig.Value.StreamSendThrottlingEnabled
        ? new StreamSendThrottle(commsConfig.Value.StreamSendBurst, commsConfig.Value.StreamSendRatePerMinute / 60d, timeProvider)
        : null;
    private long _rateLimitedSinceNotice;
    private long _staleSinceNotice;
    private long _lastDropNoticeTicks;

    /// <inheritdoc/>
    public string FeatureName => CommsFeatureNames.Comms;

    //Resolved on use rather than captured at construction, so an unreachable cache cannot stop the
    //feature being constructed; only the stream path needs it.
    private IDatabase Db => remoteCache.Db;

    //A registered responder that is not fully configured behaves as if none were registered.
    private ICommsResponder? ActiveResponder => responder is { IsAvailable: true } ? responder : null;

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        LogStarting(logger, nameof(CommunicationsBgService), !string.IsNullOrEmpty(commsConfig.Value.MonitorGroupName),
            ActiveResponder is not null);
        try
        {
            // Start consuming the comms stream immediately — this must not be gated behind the gateway
            // connection, otherwise stream events queue indefinitely until Signalizr becomes reachable.
            await EnsureConsumerGroupAsync();
            var streamTask = DrainStreamAsync(cancellationToken);

            await WaitForGroupsAsync(cancellationToken);
            _groupReady.TrySetResult();

            var replyTask = DrainReplyQueueAsync(cancellationToken);

            LogSubscribing(logger, nameof(CommunicationsBgService));
            var incomingTask = SubscribeToMessagesAsync(cancellationToken);

            //await-await-WhenAny propagates the first faulted task immediately so the
            //service crashes and the pod restarts rather than running in a degraded state.
            await await Task.WhenAny(streamTask, replyTask, incomingTask);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
        {
            LogFatalError(logger, ex, nameof(CommunicationsBgService));
            throw;
        }
        LogExiting(logger, nameof(CommunicationsBgService));
    }

    /// <summary>Waits until the Signalizr gateway serves the configured groups.</summary>
    /// <remarks>
    /// An unreachable gateway is retried, because it recovers on its own. A missing chat group
    /// is a configuration fault that retrying cannot fix, so
    /// <see cref="SignalizrClientExtensions.WaitForGroupsAsync"/> throws. A missing monitor group
    /// only degrades diagnostics, so it is logged and tolerated.
    /// </remarks>
    private async Task WaitForGroupsAsync(CancellationToken cancellationToken)
    {
        var missingOptional = await signalizrClient.WaitForGroupsAsync(
            [commsConfig.Value.GroupName],
            [commsConfig.Value.MonitorGroupName],
            TimeSpan.FromMilliseconds(commsConfig.Value.HealthCheckProbeDelayMs),
            timeProvider,
            (ex, attempt) => logger.Log(attempt % 10 == 0 ? LogLevel.Warning : LogLevel.Debug, ex,
                "{ClassName} Signalizr gateway not reachable, attempt {Attempt}, retrying in {RetryMs}ms",
                nameof(CommunicationsBgService), attempt, commsConfig.Value.HealthCheckProbeDelayMs),
            cancellationToken);

        if (missingOptional.Count > 0)
            LogMonitorGroupUnavailable(logger, nameof(CommunicationsBgService));

        LogChatGroupReady(logger, nameof(CommunicationsBgService));
    }
}
