using Microsoft.Agents.AI;

namespace CasCap.Services;

public partial class CommunicationsBgService
{
    private async Task<(AgentRunResult? Result, List<CommsDebugStep> DebugSteps)> RunAgentAsync(string prompt, byte[]? binaryContent, string? mimeType,
        bool bypassSession, string? sender, long? timestamp, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("{ClassName} running agent inference, promptLength={PromptLength}, hasAttachment={HasAttachment}, model={Model}",
                nameof(CommunicationsBgService), prompt.Length,
                binaryContent is not null, _commandHandler.ModelOverride ?? _commsAgent!.Provider);

            AgentSession? session = null;
            if (!bypassSession)
            {
                session = await _commandHandler.LoadSessionAsync(_agent!, _commsAgent!.Name);
                if (session is null)
                    _logger.LogInformation("{ClassName} new agent session started", nameof(CommunicationsBgService));
                else
                    _logger.LogInformation("{ClassName} agent session resumed", nameof(CommunicationsBgService));
            }
            else
                _logger.LogInformation("{ClassName} bypassing session for this request", nameof(CommunicationsBgService));

            // Transcribe audio attachments via the STT agent before passing to the comms agent.
            if (binaryContent is not null && mimeType is not null && mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            {
                var transcription = await TranscribeAudioAsync(binaryContent, mimeType, cancellationToken);
                if (!string.IsNullOrWhiteSpace(transcription))
                {
                    prompt = $"[AUDIO TRANSCRIPTION] The user sent an audio clip. Transcribed text: \"{transcription}\"\n\nOriginal prompt: {prompt}";
                    binaryContent = null;
                    mimeType = null;
                    _logger.LogInformation("{ClassName} audio transcribed, {TranscriptionLength} chars",
                        nameof(CommunicationsBgService), transcription.Length);
                }
                else
                    _logger.LogWarning("{ClassName} audio transcription returned empty, passing raw audio to agent",
                        nameof(CommunicationsBgService));
            }

            var message = AgentExtensions.BuildChatMessage(prompt,
                binaryContent: binaryContent, mimeType: mimeType);
            var chatOptions = AgentExtensions.BuildChatOptions(_commsAgent!, _resolvedInstructions!);
            _commandHandler.ApplyModelOverride(chatOptions);
            _commandHandler.ApplyInstructionsOverride(chatOptions, _aiConfig);

            // Accumulate debug steps across the full agent pipeline.
            var debugSteps = new List<CommsDebugStep>();
            var pipelineSw = Stopwatch.StartNew();

            debugSteps.Add(new CommsDebugStep(
                $"\U0001F680 {_commsAgent!.Name}",
                $"{_commandHandler.ModelOverride ?? _commsAgent.Provider} ({_provider!.ModelName})",
                TimeSpan.Zero));

            // Wire ambient delegation callback so sub-agent invocations notify the chat.
            AgentExtensions.SetDelegationCallback(async (agentKey, depth, subProvider, ct) =>
            {
                var depthLabel = depth switch { 1 => "sub-agent", 2 => "sub-sub-agent", _ => $"depth-{depth} agent" };
                _logger.LogInformation("{ClassName} delegation callback fired for {AgentKey} ({DepthLabel}), provider={ProviderModel}",
                    nameof(CommunicationsBgService), agentKey, depthLabel, $"{subProvider.Type}:{subProvider.ModelName}");

                debugSteps.Add(new CommsDebugStep(
                    $"\U0001F500 {agentKey} ({depthLabel})",
                    $"{subProvider.Type}:{subProvider.ModelName}",
                    pipelineSw.Elapsed));

                // Option A: send a separate status message (toggleable via config).
                if (_commsAgentConfig.DelegationMessagesEnabled)
                {
                    var statusMsg = new SignalMessageRequest
                    {
                        Message = $"\U0001F500 Consulting {agentKey} ({depthLabel}) \u2022 {subProvider.Type}:{subProvider.ModelName}",
                        Number = _signalCliConfig.PhoneNumber,
                        Recipients = [_groupId!],
                    };
                    await _notifier.SendAsync(statusMsg, ct);
                }

                // Option B: swap reaction to twisted-arrows to indicate delegation.
                if (sender is not null && timestamp is not null)
                    await _notifier.SendProgressUpdateAsync(
                        _signalCliConfig.PhoneNumber, _groupId!, "\U0001F500", sender, timestamp.Value);
            });

            // Wire ambient completion callback so sub-agent results are collected.
            AgentExtensions.SetCompletionCallback((agentKey, depth, subResult, ct) =>
            {
                debugSteps.Add(new CommsDebugStep(
                    $"\u2705 {agentKey}",
                    null,
                    pipelineSw.Elapsed,
                    subResult));
                return Task.CompletedTask;
            });

            // Wire ambient compaction callback so session compaction events are posted to debug.
            AgentExtensions.SetCompactionCallback((inputCount, outputCount, toolDropped, windowTrimmed, target) =>
            {
                _logger.LogInformation(
                    "{ClassName} session compaction: {InputCount} \u2192 {OutputCount} (tool dropped={ToolDropped}, window trimmed={WindowTrimmed}, target={Target})",
                    nameof(CommunicationsBgService), inputCount, outputCount, toolDropped, windowTrimmed, target);

                _ = _debugNotifier.SendCompactionDebugAsync(inputCount, outputCount, toolDropped, windowTrimmed, target, cancellationToken);
            });

            try
            {
                // Snapshot GPU power before inference for energy calculation.
                var preSnapshots = _edgeHardwareQuerySvc is not null ? await _edgeHardwareQuerySvc.GetLatestSnapshots() : null;
                var preSnapshot = preSnapshots?.FirstOrDefault();

                var result = await _agent!.RunAnalysisAsync(
                    _provider!,
                    _commsAgent!,
                    message,
                    chatOptions,
                    session: session,
                    cancellationToken: cancellationToken);

                // Snapshot GPU power after inference and populate energy metrics.
                var postSnapshots = _edgeHardwareQuerySvc is not null ? await _edgeHardwareQuerySvc.GetLatestSnapshots() : null;
                var postSnapshot = postSnapshots?.FirstOrDefault();
                result.PopulateEnergyMetrics(preSnapshot, postSnapshot, _edgeHardwareConfig);

                _logger.LogInformation("{ClassName} agent completed in {Duration}, session {SessionStatus}",
                    nameof(CommunicationsBgService), result.Elapsed,
                    result.Session is not null ? "present" : "missing");

                // Persist the updated session so the next call resumes conversation context.
                if (!bypassSession && result.Session is not null)
                {
                    await _commandHandler.SaveSessionAsync(_agent!, _commsAgent!.Name, result.Session);
                    _logger.LogDebug("{ClassName} agent session persisted", nameof(CommunicationsBgService));
                }

                // Restore hourglass reaction after delegation completes (Option B cleanup).
                if (sender is not null && timestamp is not null)
                    await _notifier.SendProgressUpdateAsync(
                        _signalCliConfig.PhoneNumber, _groupId!, "\u23F3", sender, timestamp.Value);

                // Final step for the parent agent.
                pipelineSw.Stop();
                debugSteps.Add(new CommsDebugStep(
                    $"\U0001F3C1 {_commsAgent!.Name}",
                    null,
                    pipelineSw.Elapsed,
                    result));

                return (result, debugSteps);
            }
            finally
            {
                AgentExtensions.ClearDelegationCallback();
                AgentExtensions.ClearCompletionCallback();
                AgentExtensions.ClearCompactionCallback();
                AgentExtensions.ClearAmbientAudioDebug();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName} agent inference failed", nameof(CommunicationsBgService));
            return (null, []);
        }
    }

    /// <summary>
    /// Transcribes audio content using the dedicated <see cref="AgentKeys.AudioAgent"/> (Whisper model).
    /// </summary>
    /// <returns>The transcribed text, or <see langword="null"/> if the STT agent is not configured or transcription fails.</returns>
    private async Task<string?> TranscribeAudioAsync(byte[] audioBytes, string mimeType, CancellationToken cancellationToken)
    {
        if (_audioAgent is null || _audioAgentConfig is null || _audioProvider is null)
        {
            _logger.LogWarning("{ClassName} audio agent not configured, skipping audio transcription",
                nameof(CommunicationsBgService));
            return null;
        }

        try
        {
            _logger.LogInformation("{ClassName} transcribing {Size} byte audio ({MimeType}) via audio agent, model={ModelName}",
                nameof(CommunicationsBgService), audioBytes.Length, mimeType, _audioProvider.ModelName);

            // Transcode non-WAV audio to WAV (matching the sub-agent delegation path)
            // so the Whisper model receives decoded PCM and we capture both files for debug.
            var wavBytes = audioBytes;
            var wavMimeType = mimeType;
            if (mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
                && !mimeType.Equals("audio/wav", StringComparison.OrdinalIgnoreCase)
                && !mimeType.Equals("audio/x-wav", StringComparison.OrdinalIgnoreCase))
            {
                var transcoded = await AgentExtensions.TranscodeToWavAsync(audioBytes, cancellationToken);
                if (transcoded is not null)
                {
                    _logger.LogInformation("{ClassName} transcoded {OriginalSize} byte {OriginalMimeType} \u2192 {TranscodedSize} byte WAV",
                        nameof(CommunicationsBgService), audioBytes.Length, mimeType, transcoded.Length);
                    wavBytes = transcoded;
                    wavMimeType = "audio/wav";
                    AgentExtensions.SetAmbientAudioDebug(audioBytes, mimeType, transcoded);
                }
                else
                {
                    _logger.LogWarning("{ClassName} ffmpeg transcode failed, sending original {MimeType} to audio agent",
                        nameof(CommunicationsBgService), mimeType);
                    AgentExtensions.SetAmbientAudioDebug(audioBytes, mimeType, transcodedWav: null);
                }
            }

            var message = AgentExtensions.BuildChatMessage(_audioAgentConfig.Prompt,
                binaryContent: wavBytes, mimeType: wavMimeType);
            var chatOptions = AgentExtensions.BuildChatOptions(_audioAgentConfig,
                _audioAgentConfig.Instructions ?? string.Empty);

            var result = await _audioAgent.RunAnalysisAsync(
                _audioProvider,
                _audioAgentConfig,
                message,
                chatOptions,
                cancellationToken: cancellationToken);

            _logger.LogInformation("{ClassName} audio transcription completed in {Duration}, outputLength={OutputLength}",
                nameof(CommunicationsBgService), result.Elapsed, result.OutputText?.Length ?? 0);

            return result.OutputText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName} audio transcription failed", nameof(CommunicationsBgService));
            return null;
        }
    }

    /// <summary>Updates the Signal profile display name to <c>{ProfileName} ({modelName})</c>, or just <c>{ProfileName}</c> when no model is known.</summary>
    /// <remarks>
    /// The signal-cli REST API splits the <c>name</c> field on <c>\0</c> into given/family name.
    /// Appending <c>\0</c> explicitly clears the family name so stale values don't persist.
    /// </remarks>
    private async Task UpdateSignalProfileNameAsync(string? modelName)
    {
        var displayName = string.IsNullOrWhiteSpace(modelName)
            ? _commsAgentConfig.ProfileName
            : $"{_commsAgentConfig.ProfileName} ({modelName})";
        // Append null separator to clear any existing Signal family name.
        var profileName = displayName + "\0";
        try
        {
            var success = await _notifier.UpdateProfileNameAsync(_signalCliConfig.PhoneNumber, profileName);
            if (success)
                _logger.LogInformation("{ClassName} Signal profile name updated to {DisplayName}",
                    nameof(CommunicationsBgService), displayName);
            else
                _logger.LogWarning("{ClassName} Signal profile name update failed for {DisplayName}",
                    nameof(CommunicationsBgService), displayName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} unable to update Signal profile name to {DisplayName}",
                nameof(CommunicationsBgService), displayName);
        }
    }
}
