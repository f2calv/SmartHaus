using Microsoft.Agents.AI;

namespace CasCap.Services;

/// <summary>
/// Encapsulates all debug and stats messaging sent to the
/// <see cref="CommsAgentConfig.MonitorChannelName"/> Signalizr channel for observability of the
/// comms agent pipeline.
/// </summary>
/// <remarks>
/// The monitor channel is operator diagnostics: it carries prompts, transcripts and tool
/// arguments, so its Signal group must contain only the operator. Leaving the channel name unset
/// disables every message sent from here.
/// </remarks>
public sealed class CommsDebugNotifier(
    ILogger<CommsDebugNotifier> logger,
    IOptions<CommsAgentConfig> commsAgentConfig,
    IOptions<EdgeHardwareConfig> edgeHardwareConfig,
    ISignalizrClient signalizrClient,
    IServiceProvider serviceProvider)
{
    /// <summary>
    /// Builds a compact stats footer to append to the group message.
    /// </summary>
    public async Task<string> FormatStatsFooterAsync(AgentRunResult result)
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
        if (result.Session is not null)
        {
            try
            {
                var entries = ChatCommandParser.GetStateBagEntries(result.Session);
                var totalBytes = entries.Sum(e => e.ByteSize);
                var userMsg = entries.Sum(e => e.UserMessageCount);
                var assistantMsg = entries.Sum(e => e.AssistantMessageCount);
                sb.Append($" | 💾 {totalBytes / 1024.0:F1}KB, {userMsg}u/{assistantMsg}a");
            }
            catch { /* context size is best-effort */ }
        }

        // Energy / GPU line.
        var solarSnapshot = await GetSolarSnapshotAsync();
        AppendEnergyLine(sb, result, solarSnapshot);

        return sb.ToString();
    }

    /// <summary>
    /// Sends the transcript of an inbound voice message to <see cref="CommsAgentConfig.MonitorChannelName"/>
    /// so a misheard command can be diagnosed against what the agent actually received.
    /// </summary>
    /// <param name="result">The successful transcription, carrying the transcript and stage timings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Only called when <see cref="CommsAgentConfig.EchoTranscriptToDebugChat"/> is enabled. The
    /// transcript goes to the monitor channel alone and never to a log sink or telemetry.
    /// </remarks>
    public async Task SendVoiceTranscriptDebugAsync(VoiceTranscriptionResult result, CancellationToken cancellationToken)
    {
        if (commsAgentConfig.Value.MonitorChannelName is not { Length: > 0 } monitorChannel)
            return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"\U0001F442 Processing audio prompt: \u201C{result.Text}\u201D");
            if (result.AudioDuration is { } audio)
                sb.Append($"\u23F1 audio {audio.TotalSeconds:N1}s | ");
            //A null transcode duration means the sender's audio was already a conforming WAV.
            sb.Append("transcode ");
            sb.Append(result.TranscodeDuration is { } transcode
                ? $"{transcode.TotalMilliseconds:N0}ms{Realtime(result.AudioDuration, transcode)}"
                : "skipped");
            if (result.TranscriptionDuration is { } transcription)
                sb.Append($" | transcribe {transcription.TotalMilliseconds:N0}ms{Realtime(result.AudioDuration, transcription)}");

            await signalizrClient.SendAsync(monitorChannel, sb.ToString(), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{ClassName} failed to send the voice transcript to channel {Channel}",
                nameof(CommsDebugNotifier), monitorChannel);
        }
    }

    //Seconds of audio processed per second of wall clock; above one means faster than playback.
    private static string Realtime(TimeSpan? audioDuration, TimeSpan elapsed) =>
        audioDuration is { } audio && elapsed > TimeSpan.Zero
            ? $" ({audio.TotalSeconds / elapsed.TotalSeconds:N1}x)"
            : string.Empty;

    /// <summary>
    /// Sends a copy of an incoming stream event to <see cref="CommsAgentConfig.MonitorChannelName"/>
    /// so automated sensor messages can be observed alongside the agent's response.
    /// </summary>
    public async Task SendStreamEventDebugAsync(CommsEvent commsEvent, CancellationToken cancellationToken)
    {
        if (commsAgentConfig.Value.MonitorChannelName is not { Length: > 0 } monitorChannel)
            return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"\U0001F4E5 {commsEvent.Source}");
            sb.AppendLine($"\u23F0 {commsEvent.TimestampUtc:u}");
            sb.AppendLine(commsEvent.Message);
            if (commsEvent.JsonPayload is not null)
                sb.AppendLine($"\U0001F4CE {commsEvent.JsonPayload}");

            await signalizrClient.SendAsync(monitorChannel, sb.ToString().TrimEnd(), cancellationToken);
            logger.LogDebug("{ClassName} stream event debug sent to channel {Channel}",
                nameof(CommsDebugNotifier), monitorChannel);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{ClassName} failed to send stream event debug to channel {Channel}",
                nameof(CommsDebugNotifier), monitorChannel);
        }
    }

    /// <summary>
    /// Sends a compaction notification to <see cref="CommsAgentConfig.MonitorChannelName"/>
    /// when the <see cref="ToolOutputStrippingChatReducer"/> trims the chat history.
    /// </summary>
    public async Task SendCompactionDebugAsync(int inputCount, int outputCount, int toolDropped, int windowTrimmed, int target,
        CancellationToken cancellationToken)
    {
        if (commsAgentConfig.Value.MonitorChannelName is not { Length: > 0 } monitorChannel)
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

            await signalizrClient.SendAsync(monitorChannel, sb.ToString(), cancellationToken);
            logger.LogDebug("{ClassName} compaction debug sent to channel {Channel}",
                nameof(CommsDebugNotifier), monitorChannel);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{ClassName} failed to send compaction debug to channel {Channel}",
                nameof(CommsDebugNotifier), monitorChannel);
        }
    }

    /// <summary>
    /// Sends a single consolidated debug message to <see cref="CommsAgentConfig.MonitorChannelName"/>
    /// containing a step-by-step timeline of the agent pipeline execution.
    /// </summary>
    /// <remarks>
    /// <c>inboundTimestamp</c> is the inbound Signal message timestamp in milliseconds since the
    /// Unix epoch, used to report the end-to-end turnaround the sender actually experienced.
    /// </remarks>
    public async Task SendDebugStatsAsync(string prompt, AgentRunResult result, List<CommsDebugStep> debugSteps,
        byte[]? originalBinaryContent, string? originalMimeType, long? inboundTimestamp,
        CancellationToken cancellationToken)
    {
        if (commsAgentConfig.Value.MonitorChannelName is not { Length: > 0 } monitorChannel)
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
                var elapsed = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(received);
                if (elapsed > TimeSpan.Zero)
                    sb.AppendLine($"\u23F1 end to end {elapsed.TotalSeconds:F1}s");
            }

            // ── Step-by-step pipeline timeline ──────────────────────────
            if (debugSteps.Count > 0)
            {
                for (var i = 0; i < debugSteps.Count; i++)
                {
                    var step = debugSteps[i];
                    var stepLine = $"{i + 1}. {step.Label}  T+{step.WallClockOffset.TotalSeconds:F1}s";
                    if (step.Result is not null)
                        stepLine += $"  \u23F1 {step.Result.Elapsed.TotalSeconds:F1}s";
                    sb.AppendLine(stepLine);

                    if (step.Provider is not null)
                        sb.AppendLine($"   \U0001F4AC {step.Provider}");

                    if (step.Result is not null)
                    {
                        var r = step.Result;
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
                        if (r.GetEstimatedEnergyWh() is > 0)
                            sb.AppendLine($"   \u26A1 {r.GetEstimatedEnergyWh():F4} Wh");
                    }
                }
                sb.AppendLine("\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500");
            }

            // ── Overall summary ─────────────────────────────────────────
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

            if (result.GetEstimatedEnergyWh() is > 0)
                sb.AppendLine($"\u26A1 Energy: {result.GetEstimatedEnergyWh():F4} Wh");
            if (result.GetGpuPowerDrawW() is > 0)
                sb.AppendLine($"\u26A1 GPU: {result.GetGpuPowerDrawW():F1} W");
            if (result.GetGpuTemperatureC() is not null)
                sb.AppendLine($"\U0001F321 GPU temp: {result.GetGpuTemperatureC():F0}\u00B0C");
            if (result.GetGpuUtilizationPercent() is not null)
                sb.AppendLine($"\u2699 GPU util: {result.GetGpuUtilizationPercent():F0}%");

            if (result.Session is not null)
            {
                try
                {
                    var entries = ChatCommandParser.GetStateBagEntries(result.Session);
                    var totalBytes = entries.Sum(e => e.ByteSize);
                    var userMsg = entries.Sum(e => e.UserMessageCount);
                    var assistantMsg = entries.Sum(e => e.AssistantMessageCount);
                    sb.AppendLine($"\U0001F4BE Session: {totalBytes / 1024.0:F1}KB, {userMsg}u/{assistantMsg}a");
                }
                catch
                {
                    sb.AppendLine("\U0001F4BE Session: detail unavailable");
                }
            }

            if (result.FinishReason is { Length: > 0 })
                sb.AppendLine($"\U0001F3C1 Finish: {result.FinishReason}");

            await signalizrClient.SendAsync(monitorChannel, sb.ToString().TrimEnd(), cancellationToken);
            logger.LogDebug("{ClassName} debug stats sent to channel {Channel}",
                nameof(CommsDebugNotifier), monitorChannel);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{ClassName} failed to send debug stats to channel {Channel}",
                nameof(CommsDebugNotifier), monitorChannel);
        }
    }

    /// <summary>
    /// Fetches the current Fronius inverter snapshot for solar context, or <see langword="null"/> on failure.
    /// </summary>
    private async Task<InverterSnapshot?> GetSolarSnapshotAsync()
    {
        try
        {
            var froniusQuerySvc = serviceProvider.GetService<IFroniusQueryService>();
            if (froniusQuerySvc is null)
                return null;
            return await froniusQuerySvc.GetInverterSnapshot();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Appends a second stats line with energy consumption and GPU metrics when available.
    /// </summary>
    private void AppendEnergyLine(StringBuilder sb, AgentRunResult result, InverterSnapshot? solarSnapshot)
    {
        var energyWh = result.GetEstimatedEnergyWh();
        var gpuTemp = result.GetGpuTemperatureC();
        var gpuUtil = result.GetGpuUtilizationPercent();
        var energyReporting = edgeHardwareConfig.Value.EnergyReporting;
        var reportEnergy = energyWh is > 0 && energyReporting is not EnergyReportingMode.Off;

        if (!reportEnergy && gpuTemp is null)
            return;

        sb.AppendLine();

        if (reportEnergy)
        {
            sb.Append($"⚡ {energyWh:F2}Wh");

            if (energyReporting is EnergyReportingMode.Verbose)
            {
                // Fun comparisons.
                var comparisons = new List<string>();
                if (edgeHardwareConfig.Value.KettleBoilWh > 0)
                    comparisons.Add($"~{energyWh!.Value / edgeHardwareConfig.Value.KettleBoilWh:F4} kettles");
                if (edgeHardwareConfig.Value.PhoneChargeWh > 0)
                    comparisons.Add($"~{energyWh!.Value / edgeHardwareConfig.Value.PhoneChargeWh:F3} phone charges");
                if (edgeHardwareConfig.Value.LedBulbHourWh > 0)
                    comparisons.Add($"~{energyWh!.Value / edgeHardwareConfig.Value.LedBulbHourWh:F3} LED-bulb-hrs");
                if (comparisons.Count > 0)
                    sb.Append($" ({string.Join(" | ", comparisons)})");
            }
        }

        if (gpuTemp is not null)
            sb.Append($" | 🌡 {gpuTemp:F0}°C");

        if (gpuUtil is not null)
            sb.Append($" | ⚙ {gpuUtil:F0}%");

        // Solar context from Fronius snapshot (best-effort).
        if (solarSnapshot is not null)
        {
            if (solarSnapshot.PhotovoltaicPower > Math.Abs(solarSnapshot.LoadPower))
                sb.Append(" | ☀\uFE0F solar-powered");
            else if (solarSnapshot.BatteryPower > 0)
                sb.Append(" | 🔋 battery-powered");
            else if (solarSnapshot.GridPower > 0)
                sb.Append(" | 🔌 grid");
        }
    }
}
