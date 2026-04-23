using Microsoft.Agents.AI;

namespace CasCap.Services;

/// <summary>
/// Encapsulates all debug and stats messaging sent to the
/// <see cref="SignalCliConfig.PhoneNumberDebug"/> number ("Note to Self") for observability
/// of the comms agent pipeline.
/// </summary>
public class CommsDebugNotifier(
    ILogger<CommsDebugNotifier> logger,
    IOptions<SignalCliConfig> signalCliConfig,
    IOptions<EdgeHardwareConfig> edgeHardwareConfig,
    INotifier notifier,
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
    /// Sends a copy of an incoming stream event to <see cref="SignalCliConfig.PhoneNumberDebug"/>
    /// so automated sensor messages can be observed alongside the agent's response.
    /// </summary>
    public async Task SendStreamEventDebugAsync(CommsEvent commsEvent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(signalCliConfig.Value.PhoneNumberDebug))
            return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"\U0001F4E5 {commsEvent.Source}");
            sb.AppendLine($"\u23F0 {commsEvent.TimestampUtc:u}");
            sb.AppendLine(commsEvent.Message);
            if (commsEvent.JsonPayload is not null)
                sb.AppendLine($"\U0001F4CE {commsEvent.JsonPayload}");

            var debugMsg = new SignalMessageRequest
            {
                Message = sb.ToString().TrimEnd(),
                Number = signalCliConfig.Value.PhoneNumber,
                Recipients = [signalCliConfig.Value.PhoneNumberDebug],
            };
            await notifier.SendAsync(debugMsg, cancellationToken);
            logger.LogDebug("{ClassName} stream event debug sent to {PhoneNumberDebug}",
                nameof(CommsDebugNotifier), signalCliConfig.Value.PhoneNumberDebug);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{ClassName} failed to send stream event debug to {PhoneNumberDebug}",
                nameof(CommsDebugNotifier), signalCliConfig.Value.PhoneNumberDebug);
        }
    }

    /// <summary>
    /// Sends a compaction notification to <see cref="SignalCliConfig.PhoneNumberDebug"/>
    /// when the <see cref="ToolOutputStrippingChatReducer"/> trims the chat history.
    /// </summary>
    public async Task SendCompactionDebugAsync(int inputCount, int outputCount, int toolDropped, int windowTrimmed, int target,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(signalCliConfig.Value.PhoneNumberDebug))
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

            var debugMsg = new SignalMessageRequest
            {
                Message = sb.ToString(),
                Number = signalCliConfig.Value.PhoneNumber,
                Recipients = [signalCliConfig.Value.PhoneNumberDebug],
            };
            await notifier.SendAsync(debugMsg, cancellationToken);
            logger.LogDebug("{ClassName} compaction debug sent to {PhoneNumberDebug}",
                nameof(CommsDebugNotifier), signalCliConfig.Value.PhoneNumberDebug);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{ClassName} failed to send compaction debug to {PhoneNumberDebug}",
                nameof(CommsDebugNotifier), signalCliConfig.Value.PhoneNumberDebug);
        }
    }

    /// <summary>
    /// Sends a single consolidated debug message to <see cref="SignalCliConfig.PhoneNumberDebug"/>
    /// containing a step-by-step timeline of the agent pipeline execution.
    /// </summary>
    public async Task SendDebugStatsAsync(string prompt, AgentRunResult result, List<CommsDebugStep> debugSteps,
        byte[]? originalBinaryContent, string? originalMimeType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(signalCliConfig.Value.PhoneNumberDebug))
            return;

        try
        {
            var sb = new StringBuilder();

            // ── Quoted prompt ───────────────────────────────────────────
            var truncated = prompt.Length > 200 ? prompt[..200] + "\u2026" : prompt;
            sb.AppendLine($"\u201C{truncated}\u201D");

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

            // Attach original + transcoded audio files when available for pipeline debugging.
            var audioAttachments = BuildAudioDebugAttachments(originalBinaryContent, originalMimeType);

            var debugMsg = new SignalMessageRequest
            {
                Message = sb.ToString().TrimEnd(),
                Number = signalCliConfig.Value.PhoneNumber,
                Recipients = [signalCliConfig.Value.PhoneNumberDebug],
                Base64Attachments = audioAttachments,
            };
            await notifier.SendAsync(debugMsg, cancellationToken);
            logger.LogDebug("{ClassName} debug stats sent to {PhoneNumberDebug}",
                nameof(CommsDebugNotifier), signalCliConfig.Value.PhoneNumberDebug);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{ClassName} failed to send debug stats to {PhoneNumberDebug}",
                nameof(CommsDebugNotifier), signalCliConfig.Value.PhoneNumberDebug);
        }
    }

    /// <summary>
    /// Builds base64 data-URI attachments for the original audio and the transcoded WAV
    /// so both can be inspected via the debug Signal message.
    /// </summary>
    /// <returns>An array of data-URI strings, or <see langword="null"/> when no audio content is available.</returns>
    internal static string[]? BuildAudioDebugAttachments(byte[]? originalBinaryContent, string? originalMimeType)
    {
        if (originalBinaryContent is null || originalMimeType is null
            || !originalMimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            return null;

        var attachments = new List<string>();

        // Derive a short extension from the MIME type (e.g. "audio/aac" → "aac", "audio/ogg" → "ogg").
        var ext = originalMimeType.AsSpan()[(originalMimeType.IndexOf('/') + 1)..].ToString();
        attachments.Add(
            $"data:{originalMimeType};filename=original.{ext};base64,{Convert.ToBase64String(originalBinaryContent)}");

        // Check for transcoded WAV from the sub-agent delegation pipeline.
        var audioDebug = AgentExtensions.GetAmbientAudioDebug();
        if (audioDebug?.TranscodedWav is { } wavBytes)
            attachments.Add(
                $"data:audio/wav;filename=transcoded.wav;base64,{Convert.ToBase64String(wavBytes)}");

        return attachments.Count > 0 ? [.. attachments] : null;
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

        if ((energyWh is null or 0) && gpuTemp is null)
            return;

        sb.AppendLine();

        if (energyWh is > 0)
        {
            sb.Append($"⚡ {energyWh:F2}Wh");

            // Fun comparisons.
            var comparisons = new List<string>();
            if (edgeHardwareConfig.Value.KettleBoilWh > 0)
                comparisons.Add($"~{energyWh.Value / edgeHardwareConfig.Value.KettleBoilWh:F4} kettles");
            if (edgeHardwareConfig.Value.PhoneChargeWh > 0)
                comparisons.Add($"~{energyWh.Value / edgeHardwareConfig.Value.PhoneChargeWh:F3} phone charges");
            if (edgeHardwareConfig.Value.LedBulbHourWh > 0)
                comparisons.Add($"~{energyWh.Value / edgeHardwareConfig.Value.LedBulbHourWh:F3} LED-bulb-hrs");
            if (comparisons.Count > 0)
                sb.Append($" ({string.Join(" | ", comparisons)})");
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
