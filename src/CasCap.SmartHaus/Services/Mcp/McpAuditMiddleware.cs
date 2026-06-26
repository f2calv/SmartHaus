using CasCap.Attributes;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Collections.Frozen;
using System.Text.Json;

namespace CasCap.Services;

/// <summary>
/// Function invocation middleware that audits every MCP tool call and optionally
/// gates execution behind a human-in-the-loop approval step.
/// </summary>
/// <remarks>
/// <para>
/// Audit records are emitted via structured logging at <see cref="LogLevel.Information"/>
/// and (when Redis is available) appended to a Redis Stream for durable queryable history.
/// </para>
/// <para>
/// The approval gate inspects the <see cref="RequiresApprovalAttribute"/> on the underlying
/// method and the <see cref="AuditConfig.AlwaysRequireApproval"/> / <see cref="AuditConfig.NeverRequireApproval"/>
/// config overrides to determine whether to pause execution and poll the user via Signal.
/// </para>
/// </remarks>
public sealed class McpAuditMiddleware(
    ILogger<McpAuditMiddleware> logger,
    IOptionsMonitor<AuditConfig> auditConfig,
    IPollTracker pollTracker,
    SignalCliRestClientService signalCliSvc,
    IOptions<SignalCliConfig> signalCliConfig,
    IOptions<CommsAgentConfig> commsAgentConfig)
{
    /// <summary>
    /// Cached set of tool names (PascalCase method names) that carry <see cref="RequiresApprovalAttribute"/>.
    /// Built lazily on first invocation from all <see cref="McpServerToolTypeAttribute"/> classes.
    /// </summary>
    private FrozenDictionary<string, RequiresApprovalAttribute>? _approvalLookup;

    /// <summary>JSON serializer options for argument logging.</summary>
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// The middleware entry point matching the <c>AIAgentBuilder.Use(...)</c> delegate signature.
    /// </summary>
    public async ValueTask<object?> InvokeAsync(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        var config = auditConfig.CurrentValue;
        if (!config.Enabled)
            return await next(context, cancellationToken);

        var toolName = context.Function.Name;
        var argsJson = SerializeArguments(context.Arguments);
        var timestampUtc = DateTime.UtcNow;

        // ── Approval gate ────────────────────────────────────────────────────
        var approvalRequired = IsApprovalRequired(toolName, config);
        bool? approvalGranted = null;

        if (approvalRequired && config.ApprovalEnabled)
        {
            approvalGranted = await RequestApprovalAsync(toolName, argsJson, config, cancellationToken);

            if (approvalGranted != true)
            {
                var deniedRecord = new McpAuditRecord
                {
                    TimestampUtc = timestampUtc,
                    ToolName = toolName,
                    Arguments = argsJson,
                    Duration = TimeSpan.Zero,
                    Success = false,
                    ErrorType = "ApprovalDenied",
                    ErrorMessage = approvalGranted == false ? "User denied the action." : "Approval timed out.",
                    ApprovalRequired = true,
                    ApprovalGranted = false,
                };

                LogAuditRecord(deniedRecord);
                return $"Action denied: {deniedRecord.ErrorMessage}";
            }
        }

        // ── Execute the tool ─────────────────────────────────────────────────
        var sw = Stopwatch.StartNew();
        object? result;
        McpAuditRecord auditRecord;

        try
        {
            result = await next(context, cancellationToken);
            sw.Stop();

            auditRecord = new McpAuditRecord
            {
                TimestampUtc = timestampUtc,
                ToolName = toolName,
                Arguments = argsJson,
                Duration = sw.Elapsed,
                Success = true,
                ApprovalRequired = approvalRequired,
                ApprovalGranted = approvalRequired ? approvalGranted : null,
            };
        }
        catch (Exception ex)
        {
            sw.Stop();

            auditRecord = new McpAuditRecord
            {
                TimestampUtc = timestampUtc,
                ToolName = toolName,
                Arguments = argsJson,
                Duration = sw.Elapsed,
                Success = false,
                ErrorType = ex.GetType().Name,
                ErrorMessage = ex.Message,
                ApprovalRequired = approvalRequired,
                ApprovalGranted = approvalRequired ? approvalGranted : null,
            };

            LogAuditRecord(auditRecord);
            throw;
        }

        LogAuditRecord(auditRecord);
        return result;
    }

    #region private helpers

    private void LogAuditRecord(McpAuditRecord record)
    {
        if (record.Success)
        {
            logger.LogInformation(
                "{ClassName} tool={ToolName} duration={Duration} args={Arguments} approvalRequired={ApprovalRequired} approvalGranted={ApprovalGranted}",
                nameof(McpAuditMiddleware), record.ToolName, record.Duration, record.Arguments,
                record.ApprovalRequired, record.ApprovalGranted);
        }
        else
        {
            logger.LogWarning(
                "{ClassName} tool={ToolName} duration={Duration} args={Arguments} error={ErrorType}: {ErrorMessage} approvalRequired={ApprovalRequired} approvalGranted={ApprovalGranted}",
                nameof(McpAuditMiddleware), record.ToolName, record.Duration, record.Arguments,
                record.ErrorType, record.ErrorMessage, record.ApprovalRequired, record.ApprovalGranted);
        }
    }

    private bool IsApprovalRequired(string toolName, AuditConfig config)
    {
        // Config-driven overrides take precedence.
        if (config.NeverRequireApproval.Contains(toolName, StringComparer.OrdinalIgnoreCase))
            return false;

        if (config.AlwaysRequireApproval.Contains(toolName, StringComparer.OrdinalIgnoreCase))
            return true;

        // Fall back to attribute-based lookup.
        var lookup = GetApprovalLookup();
        return lookup.ContainsKey(toolName);
    }

    private FrozenDictionary<string, RequiresApprovalAttribute> GetApprovalLookup()
    {
        if (_approvalLookup is not null)
            return _approvalLookup;

        var dict = new Dictionary<string, RequiresApprovalAttribute>(StringComparer.OrdinalIgnoreCase);

        // Scan all McpServerToolType classes in this assembly for [RequiresApproval] methods.
        var assembly = typeof(McpAuditMiddleware).Assembly;
        var toolTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() is not null);

        foreach (var type in toolTypes)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<RequiresApprovalAttribute>();
                if (attr is not null)
                    dict[method.Name] = attr;
            }
        }

        _approvalLookup = dict.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        return _approvalLookup;
    }

    private async Task<bool?> RequestApprovalAsync(string toolName, string argsJson, AuditConfig config, CancellationToken cancellationToken)
    {
        var phoneNumber = signalCliConfig.Value.PhoneNumber;
        var groupName = commsAgentConfig.Value.GroupName;

        // Resolve the group ID.
        var groups = await signalCliSvc.ListGroups(phoneNumber);
        var groupId = groups?.FirstOrDefault(g => g.Name == groupName)?.Id;
        if (groupId is null)
        {
            logger.LogWarning("{ClassName} cannot resolve Signal group {GroupName} for approval poll, denying by default",
                nameof(McpAuditMiddleware), groupName);
            return null;
        }

        // Create the approval poll.
        var question = $"\u26a0\ufe0f Agent wants to call '{toolName}'. Approve?";
        var answers = new[] { "Yes", "No" };
        var request = new CreatePollRequest
        {
            Question = question,
            Answers = answers,
            Recipient = groupId,
        };

        var response = await signalCliSvc.CreatePoll(phoneNumber, request);
        if (response is null)
        {
            logger.LogWarning("{ClassName} failed to create approval poll for {ToolName}, denying by default",
                nameof(McpAuditMiddleware), toolName);
            return null;
        }

        pollTracker.TrackPoll(response.Timestamp, question, answers, groupId);

        // Poll for votes until timeout.
        var timeoutMs = config.ApprovalTimeoutMs;
        var pollInterval = TimeSpan.FromMilliseconds(2000);
        var elapsed = 0;

        while (elapsed < timeoutMs)
        {
            await Task.Delay(pollInterval, cancellationToken);
            elapsed += (int)pollInterval.TotalMilliseconds;

            var poll = pollTracker.GetPoll(response.Timestamp);
            if (poll is null || poll.Votes.Count == 0)
                continue;

            // Check the first vote received.
            var firstVote = poll.Votes.Values.First();
            var votedYes = firstVote.Any(i => i == 0); // Index 0 = "Yes"

            logger.LogInformation("{ClassName} approval poll for {ToolName} received vote: {Approved}",
                nameof(McpAuditMiddleware), toolName, votedYes);

            // Clean up the poll.
            pollTracker.RemovePoll(response.Timestamp);

            return votedYes;
        }

        // Timeout — deny by default (fail-safe).
        logger.LogWarning("{ClassName} approval poll for {ToolName} timed out after {TimeoutMs}ms, denying",
            nameof(McpAuditMiddleware), toolName, timeoutMs);
        pollTracker.RemovePoll(response.Timestamp);

        return null;
    }

    private static string SerializeArguments(IReadOnlyDictionary<string, object?> arguments)
    {
        if (arguments.Count == 0)
            return "{}";

        try
        {
            return JsonSerializer.Serialize(arguments, s_jsonOptions);
        }
        catch
        {
            // Fallback for non-serializable arguments.
            return string.Join(", ", arguments.Select(x => $"{x.Key}={x.Value}"));
        }
    }

    #endregion
}
