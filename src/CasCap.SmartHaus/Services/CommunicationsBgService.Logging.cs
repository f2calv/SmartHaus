namespace CasCap.Services;

public sealed partial class CommunicationsBgService
{
    // ── Stream partial ───────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} created consumer group {ConsumerGroup} on stream {StreamKey}")]
    private static partial void LogConsumerGroupCreated(ILogger logger, string className, string consumerGroup, string streamKey);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "{ClassName} consumer group {ConsumerGroup} already exists")]
    private static partial void LogConsumerGroupExists(ILogger logger, string className, string consumerGroup);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} consuming stream {StreamKey} as {ConsumerGroup}/{ConsumerName}")]
    private static partial void LogStreamConsuming(ILogger logger, string className, string streamKey, string consumerGroup, string consumerName);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{ClassName} consumer group disappeared, recreating")]
    private static partial void LogConsumerGroupDisappeared(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "{ClassName} error during stream read cycle")]
    private static partial void LogStreamReadError(ILogger logger, Exception ex, string className);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} processing stream event from {Source}: {Message}")]
    private static partial void LogProcessingStreamEvent(ILogger logger, string className, string source, string message);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} no agent configured, forwarding event directly to group")]
    private static partial void LogNoAgentForwarding(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{ClassName} direct forward SendAsync returned null — message may not have been delivered")]
    private static partial void LogDirectForwardFailed(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "{ClassName} attached {Size} byte media from {MediaRedisKey}")]
    private static partial void LogMediaAttached(ILogger logger, string className, int size, string mediaRedisKey);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{ClassName} media not found at {MediaRedisKey}, sending without attachment")]
    private static partial void LogMediaNotFound(ILogger logger, string className, string mediaRedisKey);

    // ── Messaging partial (poll loop) ────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} message polling loop started")]
    private static partial void LogPollingStarted(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "{ClassName} received {Count} envelope(s) from notifier")]
    private static partial void LogEnvelopesReceived(ILogger logger, string className, int count);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "{ClassName} envelope: Type={EnvelopeType}, HasContent={HasContent}, GroupId={GroupId}, Sender={Sender}")]
    private static partial void LogEnvelopeDetail(ILogger logger, string className, string envelopeType, bool hasContent, string? groupId, string? sender);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} processed {Processed} of {Total} envelope(s)")]
    private static partial void LogEnvelopesProcessed(ILogger logger, string className, int processed, int total);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "{ClassName} discarded {Total} non-content envelope(s)")]
    private static partial void LogEnvelopesDiscarded(ILogger logger, string className, int total);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "{ClassName} error during poll cycle ({ExceptionType}: {ExceptionMessage})")]
    private static partial void LogPollCycleError(ILogger logger, Exception ex, string className, string exceptionType, string exceptionMessage);

    // ── Messaging partial (inbound processing) ───────────────────────────

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{ClassName} received content-only message with no text or attachments from {Sender}, extension data keys: [{ExtensionKeys}], raw: {RawData}")]
    private static partial void LogContentOnlyNoText(ILogger logger, string className, string? sender, string extensionKeys, string rawData);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} received message from {Sender}: {MessageText}")]
    private static partial void LogInboundMessage(ILogger logger, string className, string? sender, string? messageText);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "{ClassName} no agent configured, skipping processing")]
    private static partial void LogNoAgentSkipping(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{ClassName} message has {Count} attachments, only the first will be processed")]
    private static partial void LogMultipleAttachments(ILogger logger, string className, int count);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} downloaded attachment {AttachmentId} ({ContentType}, {Size} bytes)")]
    private static partial void LogAttachmentDownloaded(ILogger logger, string className, string attachmentId, string? contentType, int size);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} processing slash command {Command} from {Sender}")]
    private static partial void LogSlashCommand(ILogger logger, string className, ChatCommand command, string? sender);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} enqueued reply")]
    private static partial void LogReplyEnqueued(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} received poll vote from {Voter} on poll {PollId}, selected indices: [{SelectedIndices}]")]
    private static partial void LogPollVoteReceived(ILogger logger, string className, string voter, string pollId, string selectedIndices);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "{ClassName} poll {PollId} not tracked, ignoring vote")]
    private static partial void LogPollNotTracked(ILogger logger, string className, string pollId);

    // ── Messaging partial (reply queue) ──────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} reply queue consumer started")]
    private static partial void LogReplyQueueStarted(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} processing reply")]
    private static partial void LogProcessingReply(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} sending agent response ({Length} chars, {AttachmentCount} attachment(s)) to group {GroupId}")]
    private static partial void LogSendingAgentResponse(ILogger logger, string className, int length, int attachmentCount, string? groupId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} message sent successfully, timestamp={Timestamp}")]
    private static partial void LogMessageSent(ILogger logger, string className, string timestamp);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{ClassName} SendAsync returned null — message may not have been delivered")]
    private static partial void LogSendReturnedNull(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} debug stats: parentUsage={HasUsage}, inputTokens={InputTokens}, outputTokens={OutputTokens}, debugSteps={StepCount}, stepsWithResult={StepsWithResult}, stepsWithUsage={StepsWithUsage}")]
    private static partial void LogDebugStats(ILogger logger, string className, bool hasUsage, int? inputTokens, int? outputTokens, int stepCount, int stepsWithResult, int stepsWithUsage);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{ClassName} agent returned empty response for prompt")]
    private static partial void LogAgentEmptyResponse(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "{ClassName} error processing queued reply ({ExceptionType}: {ExceptionMessage})")]
    private static partial void LogReplyProcessingError(ILogger logger, Exception ex, string className, string exceptionType, string exceptionMessage);
}
