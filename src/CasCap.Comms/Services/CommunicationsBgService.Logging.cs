namespace CasCap.Services;

/// <summary>Source-generated log messages for <see cref="CommunicationsBgService"/>.</summary>
/// <remarks>
/// Sender identifiers, group names and message text are personal data, so inbound messages are
/// logged by length and attachment count only.
/// </remarks>
public sealed partial class CommunicationsBgService
{
    // ── Service shell ────────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} starting, monitorEnabled={MonitorEnabled}, responderEnabled={ResponderEnabled}")]
    private static partial void LogStarting(ILogger logger, string className, bool monitorEnabled, bool responderEnabled);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} subscribing to the configured Signalizr chat group")]
    private static partial void LogSubscribing(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "{ClassName} fatal error during execution")]
    private static partial void LogFatalError(ILogger logger, Exception ex, string className);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} exiting")]
    private static partial void LogExiting(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{ClassName} configured Signalizr monitor group is unavailable, diagnostics will not be delivered")]
    private static partial void LogMonitorGroupUnavailable(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} configured Signalizr chat group is ready")]
    private static partial void LogChatGroupReady(ILogger logger, string className);

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
        Message = "{ClassName} sent stream event from {Source} directly, timestamp={Timestamp}")]
    private static partial void LogStreamEventSent(ILogger logger, string className, string source, string timestamp);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "{ClassName} attached {Size} byte media from {MediaRedisKey}")]
    private static partial void LogMediaAttached(ILogger logger, string className, int size, string mediaRedisKey);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{ClassName} media {MediaRedisKey} not found or expired, sending without it")]
    private static partial void LogMediaNotFound(ILogger logger, string className, string mediaRedisKey);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{ClassName} could not fetch the event media ({ExceptionType}), sending without it")]
    private static partial void LogMediaFetchFailed(ILogger logger, Exception ex, string className, string exceptionType);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{ClassName} stream event from {Source} suppressed by rate limiter ({SuppressedCount} suppressed since last notice)")]
    private static partial void LogStreamEventSuppressed(ILogger logger, string className, string source, long suppressedCount);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{ClassName} stream event from {Source} dropped as stale ({AgeSeconds}s old, {StaleCount} stale since last notice)")]
    private static partial void LogStreamEventStale(ILogger logger, string className, string source, long ageSeconds, long staleCount);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} sent drop notice, {DroppedCount} message(s) dropped, delivered={Delivered}")]
    private static partial void LogDropNoticeSent(ILogger logger, string className, long droppedCount, bool delivered);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{ClassName} failed to send drop notice ({ExceptionType}: {ExceptionMessage})")]
    private static partial void LogDropNoticeFailed(ILogger logger, Exception ex, string className, string exceptionType, string exceptionMessage);

    // ── Messaging partial ────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} message subscription loop started")]
    private static partial void LogPollingStarted(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "{ClassName} error during subscription ({ExceptionType}: {ExceptionMessage})")]
    private static partial void LogPollCycleError(ILogger logger, Exception ex, string className, string exceptionType, string exceptionMessage);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} no responder available, ignoring message of {MessageLength} character(s) and {AttachmentCount} attachment(s)")]
    private static partial void LogInboundIgnored(ILogger logger, string className, int messageLength, int attachmentCount);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} received message of {MessageLength} character(s) and {AttachmentCount} attachment(s)")]
    private static partial void LogInboundMessage(ILogger logger, string className, int messageLength, int attachmentCount);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} duplicate message suppressed, already reserved")]
    private static partial void LogDuplicateSuppressed(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{ClassName} message has {Count} attachments, only the first will be processed")]
    private static partial void LogMultipleAttachments(ILogger logger, string className, int count);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} voice message rejected, voice processing is disabled")]
    private static partial void LogVoiceRejected(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} downloaded attachment ({ContentType}, {Size} bytes)")]
    private static partial void LogAttachmentDownloaded(ILogger logger, string className, string? contentType, int size);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} voice transcription outcome={Outcome}, transcriptChars={TranscriptChars}")]
    private static partial void LogVoiceTranscription(ILogger logger, string className, string outcome, int transcriptChars);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} voice message acquired but no turn produced, mode={VoiceProcessingMode}")]
    private static partial void LogVoiceTurnSuppressed(ILogger logger, string className, string voiceProcessingMode);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{ClassName} failed to send the voice transcript to the configured monitor group")]
    private static partial void LogTranscriptEchoFailed(ILogger logger, Exception ex, string className);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{ClassName} could not show {Interaction} feedback in the configured chat group")]
    private static partial void LogGroupInteractionFailed(ILogger logger, Exception ex, string className, string interaction);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} enqueued turn")]
    private static partial void LogReplyEnqueued(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} reply queue consumer started")]
    private static partial void LogReplyQueueStarted(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} processing turn")]
    private static partial void LogProcessingReply(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} sending reply ({Length} chars, {AttachmentCount} attachment(s)) to the configured chat group")]
    private static partial void LogSendingReply(ILogger logger, string className, int length, int attachmentCount);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} message sent successfully, timestamp={Timestamp}")]
    private static partial void LogMessageSent(ILogger logger, string className, string timestamp);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{ClassName} responder returned no reply for the turn")]
    private static partial void LogEmptyReply(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "{ClassName} error processing queued turn ({ExceptionType}: {ExceptionMessage})")]
    private static partial void LogReplyProcessingError(ILogger logger, Exception ex, string className, string exceptionType, string exceptionMessage);
}
