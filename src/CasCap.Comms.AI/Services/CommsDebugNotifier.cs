namespace CasCap.Services;

/// <summary>
/// Encapsulates the debug and stats messaging sent to the <see cref="CommsConfig.MonitorGroupName"/>
/// Signalizr group for observability of the agent pipeline.
/// </summary>
/// <remarks>
/// The monitor group is operator diagnostics: it carries prompts and tool arguments, so its Signal
/// group must contain only the operator. Leaving the group name unset disables every message sent
/// from here. Host-specific measurements come from the registered <see cref="IAgentRunEnricher"/> instances.
/// </remarks>
public sealed class CommsDebugNotifier(
    ILogger<CommsDebugNotifier> logger,
    IOptions<CommsConfig> commsConfig,
    TimeProvider timeProvider,
    ISignalizrClient signalizrClient,
    IEnumerable<IAgentRunEnricher> enrichers)
{
    /// <summary>Builds a compact stats footer to append to the group message.</summary>
    public async Task<string> FormatStatsFooterAsync(AgentRunResult result, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("───");
        sb.Append($"⏱ {result.Elapsed.TotalSeconds:F1}s");

        if (result.Usage is not null)
        {
            if (result.Usage.InputTokenCount is > 0)
                sb.Append($" | ⬆ {result.Usage.InputTokenCount:N0}");
            if (result.Usage.OutputTokenCount is > 0)
                sb.Append($" | ⬇ {result.Usage.OutputTokenCount:N0}");
            if (result.Usage.TotalTokenCount is > 0)
                sb.Append($" | Σ {result.Usage.TotalTokenCount:N0}");
        }

        if (result.ToolCallCount > 0)
        {
            sb.Append($" | 🔧 {result.ToolCallCount}");
            if (result.ToolCalls.Count > 0)
                sb.Append($" ({string.Join(", ", result.ToolCalls.Select(t => t.Name).Distinct())})");
        }

        // Session context size (best-effort).
        if (result.Session is not null && TryDescribeSession(result.Session) is { } session)
            sb.Append($" | 💾 {session}");

        // Host-specific lines, such as energy and GPU metrics.
        foreach (var enricher in enrichers)
        {
            if (await enricher.FormatFooterLineAsync(result, cancellationToken) is { Length: > 0 } line)
            {
                sb.AppendLine();
                sb.Append(line);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Sends a copy of an incoming stream event to <see cref="CommsConfig.MonitorGroupName"/>
    /// so automated sensor messages can be observed alongside the agent's response.
    /// </summary>
    public async Task SendStreamEventDebugAsync(CommsEvent commsEvent, CancellationToken cancellationToken)
    {
        if (commsConfig.Value.MonitorGroupName is not { Length: > 0 } monitorGroupName)
            return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"\U0001F4E5 {commsEvent.Source}");
            sb.AppendLine($"\u23F0 {commsEvent.TimestampUtc:u}");
            sb.AppendLine(commsEvent.Message);
            if (commsEvent.JsonPayload is not null)
                sb.AppendLine($"\U0001F4CE {commsEvent.JsonPayload}");

            await signalizrClient.SendAsync(monitorGroupName, sb.ToString().TrimEnd(), cancellationToken);
            logger.LogDebug("{ClassName} stream event debug sent to the configured monitor group",
                nameof(CommsDebugNotifier));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{ClassName} failed to send stream event debug to the configured monitor group",
                nameof(CommsDebugNotifier));
        }
    }

    /// <summary>
    /// Sends a compaction notification to <see cref="CommsConfig.MonitorGroupName"/>
    /// when the <see cref="ToolOutputStrippingChatReducer"/> trims the chat history.
    /// </summary>
    public async Task SendCompactionDebugAsync(int inputCount, int outputCount, int toolDropped, int windowTrimmed, int target,
        CancellationToken cancellationToken)
    {
        if (commsConfig.Value.MonitorGroupName is not { Length: > 0 } monitorGroupName)
            return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("\U0001F9F9 Session compaction");
            sb.AppendLine($"{inputCount} \u2192 {outputCount} messages");
            if (toolDropped > 0)
                sb.AppendLine($"\U0001F527 Tool-only dropped: {toolDropped}");
            if (windowTrimmed > 0)
                sb.AppendLine($"\u2702\uFE0F Window trimmed: {windowTrimmed}");
            sb.Append($"\U0001F3AF Target: {target}");

            await signalizrClient.SendAsync(monitorGroupName, sb.ToString(), cancellationToken);
            logger.LogDebug("{ClassName} compaction debug sent to the configured monitor group",
                nameof(CommsDebugNotifier));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{ClassName} failed to send compaction debug to the configured monitor group",
                nameof(CommsDebugNotifier));
        }
    }

    /// <summary>
    /// Sends a single consolidated debug message to <see cref="CommsConfig.MonitorGroupName"/>
    /// containing a step-by-step timeline of the agent pipeline execution.
    /// </summary>
    /// <remarks>
    /// <c>inboundTimestamp</c> is the inbound Signal message timestamp in milliseconds since the
    /// Unix epoch, used to report the end-to-end turnaround the sender actually experienced.
    /// </remarks>
    public async Task SendDebugStatsAsync(string prompt, AgentRunResult result, IReadOnlyList<CommsDebugStep> debugSteps,
        long? inboundTimestamp, CancellationToken cancellationToken)
    {
        if (commsConfig.Value.MonitorGroupName is not { Length: > 0 } monitorGroupName)
            return;

        try
        {
            var sb = new StringBuilder();

            // ── Quoted prompt ──────────────────────────────────────
            var truncated = prompt.Length > 200 ? prompt[..200] + "\u2026" : prompt;
            sb.AppendLine($"\u201C{truncated}\u201D");

            // ── End-to-end turnaround as the sender experienced it ────
            if (inboundTimestamp is { } received)
            {
                var elapsed = timeProvider.GetUtcNow() - DateTimeOffset.FromUnixTimeMilliseconds(received);
                if (elapsed > TimeSpan.Zero)
                    sb.AppendLine($"\u23F1 end to end {elapsed.TotalSeconds:F1}s");
            }

            // ── Step-by-step pipeline timeline ──────────────────────────
            if (debugSteps.Count > 0)
            {
                for (var i = 0; i < debugSteps.Count; i++)
                    AppendStep(sb, i + 1, debugSteps[i]);
                sb.AppendLine("\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500");
            }

            // ── Overall summary ─────────────────────────────────────────
            AppendSummary(sb, result);

            await signalizrClient.SendAsync(monitorGroupName, sb.ToString().TrimEnd(), cancellationToken);
            logger.LogDebug("{ClassName} debug stats sent to the configured monitor group",
                nameof(CommsDebugNotifier));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{ClassName} failed to send debug stats to the configured monitor group",
                nameof(CommsDebugNotifier));
        }
    }

    private void AppendSummary(StringBuilder sb, AgentRunResult result)
    {
        sb.AppendLine($"\u23F1 Wall: {result.Elapsed.TotalSeconds:F1}s");

        if (result.Usage is not null)
        {
            var inp = result.Usage.InputTokenCount?.ToString("N0") ?? "\u2014";
            var outp = result.Usage.OutputTokenCount?.ToString("N0") ?? "\u2014";
            var total = result.Usage.TotalTokenCount?.ToString("N0") ?? "\u2014";
            sb.AppendLine($"\u2B06 {inp} | \u2B07 {outp} | \u03A3 {total}");
            if (result.Usage.ReasoningTokenCount is > 0)
                sb.AppendLine($"\U0001F9E0 Reasoning: {result.Usage.ReasoningTokenCount.Value:N0}");
        }

        sb.AppendLine($"\U0001F4DD Output: {result.OutputText.Length:N0} chars");

        foreach (var line in enrichers.SelectMany(e => e.FormatDebugLines(result)))
            sb.AppendLine(line);

        if (result.Session is not null)
            sb.AppendLine($"\U0001F4BE Session: {TryDescribeSession(result.Session) ?? "detail unavailable"}");

        if (result.FinishReason is { Length: > 0 })
            sb.AppendLine($"\U0001F3C1 Finish: {result.FinishReason}");
    }

    private void AppendStep(StringBuilder sb, int number, CommsDebugStep step)
    {
        var stepLine = $"{number}. {step.Label}  T+{step.WallClockOffset.TotalSeconds:F1}s";
        if (step.Result is not null)
            stepLine += $"  \u23F1 {step.Result.Elapsed.TotalSeconds:F1}s";
        sb.AppendLine(stepLine);

        if (step.Provider is not null)
            sb.AppendLine($"   \U0001F4AC {step.Provider}");

        if (step.Result is not { } r)
            return;

        if (r.Usage is not null)
        {
            var inp = r.Usage.InputTokenCount?.ToString("N0") ?? "\u2014";
            var outp = r.Usage.OutputTokenCount?.ToString("N0") ?? "\u2014";
            sb.AppendLine($"   \u2B06{inp} \u2B07{outp}");
        }

        if (r.ToolCallCount > 0)
        {
            foreach (var tc in r.ToolCalls)
            {
                var args = tc.Arguments is { Count: > 0 }
                    ? $"({string.Join(", ", tc.Arguments.Select(a => $"{a.Key}={a.Value}"))})"
                    : string.Empty;
                sb.AppendLine($"   \U0001F527 {tc.Name}{args}");
            }
        }

        foreach (var line in enrichers.SelectMany(e => e.FormatDebugLines(r)))
            sb.AppendLine($"   {line}");
    }

    //Session context size is best-effort diagnostics; a state bag it cannot read is reported as unavailable.
    private static string? TryDescribeSession(AgentSession session)
    {
        try
        {
            var entries = ChatCommandParser.GetStateBagEntries(session);
            var totalBytes = entries.Sum(e => e.ByteSize);
            var userMsg = entries.Sum(e => e.UserMessageCount);
            var assistantMsg = entries.Sum(e => e.AssistantMessageCount);
            return $"{totalBytes / 1024.0:F1}KB, {userMsg}u/{assistantMsg}a";
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }
}
