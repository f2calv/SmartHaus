namespace CasCap.Services;

/// <summary>Source-generated log messages for <see cref="AgentCommsResponder"/>.</summary>
/// <remarks>Sender identifiers and message text are personal data and are never logged here.</remarks>
public sealed partial class AgentCommsResponder
{
    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{ClassName} agent profile {ProfileKey} not fully configured, agent responses disabled")]
    private static partial void LogAgentNotConfigured(ILogger logger, string className, string profileKey);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} received poll vote on poll {PollId}, selected indices: [{SelectedIndices}]")]
    private static partial void LogPollVoteReceived(ILogger logger, string className, string pollId, string selectedIndices);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "{ClassName} poll {PollId} not tracked, ignoring vote")]
    private static partial void LogPollNotTracked(ILogger logger, string className, string pollId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} processing slash command {Command}")]
    private static partial void LogSlashCommand(ILogger logger, string className, ChatCommand command);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} running agent inference, promptLength={PromptLength}, hasAttachment={HasAttachment}, model={Model}")]
    private static partial void LogAgentInferenceStarting(ILogger logger, string className, int promptLength, bool hasAttachment, string model);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} new agent session started")]
    private static partial void LogAgentSessionStarted(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} agent session resumed")]
    private static partial void LogAgentSessionResumed(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} bypassing session for this request")]
    private static partial void LogAgentSessionBypassed(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} delegating to {AgentKey} ({DepthLabel}), provider={ProviderModel}")]
    private static partial void LogAgentDelegating(ILogger logger, string className, string agentKey, string depthLabel, string providerModel);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} session compaction: {InputCount} \u2192 {OutputCount} (tool dropped={ToolDropped}, window trimmed={WindowTrimmed}, target={Target})")]
    private static partial void LogSessionCompaction(ILogger logger, string className, int inputCount, int outputCount, int toolDropped, int windowTrimmed, int target);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} agent completed in {Duration}, session {SessionStatus}")]
    private static partial void LogAgentCompleted(ILogger logger, string className, TimeSpan duration, string sessionStatus);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "{ClassName} agent session persisted")]
    private static partial void LogAgentSessionPersisted(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "{ClassName} agent inference failed")]
    private static partial void LogAgentInferenceFailed(ILogger logger, Exception ex, string className);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "{ClassName} debug stats: parentUsage={HasUsage}, inputTokens={InputTokens}, outputTokens={OutputTokens}, debugSteps={StepCount}, stepsWithResult={StepsWithResult}, stepsWithUsage={StepsWithUsage}")]
    private static partial void LogDebugStats(ILogger logger, string className, bool hasUsage, long? inputTokens, long? outputTokens, int stepCount, int stepsWithResult, int stepsWithUsage);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{ClassName} could not show delegation feedback in the configured chat group")]
    private static partial void LogGroupInteractionFailed(ILogger logger, Exception ex, string className);
}
