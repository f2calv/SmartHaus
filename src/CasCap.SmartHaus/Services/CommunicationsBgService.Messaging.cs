using System.Text.Json;

namespace CasCap.Services;

public sealed partial class CommunicationsBgService
{
    private async Task PollForMessagesAsync(CancellationToken cancellationToken)
    {
        LogPollingStarted(_logger, nameof(CommunicationsBgService));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var messages = await _notifier.ReceiveAsync(_signalCliConfig.PhoneNumber, cancellationToken);
                await ProcessEnvelopesAsync(messages, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
            {
                LogPollCycleError(_logger, ex, nameof(CommunicationsBgService), ex.GetType().Name, ex.Message);
            }

            // In JsonRpc mode ReceiveAsync blocks until messages arrive, so no polling delay
            // is needed. In REST mode the delay prevents excessive HTTP requests.
            if (_signalCliConfig.TransportMode is not (SignalCliTransport.JsonRpc or SignalCliTransport.JsonRpcNative))
                await Task.Delay(TimeSpan.FromMilliseconds(_commsAgentConfig.PollingIntervalMs), cancellationToken);
        }
    }

    /// <summary>
    /// Applies the group/echo filter to a batch of received envelopes and routes each qualifying
    /// envelope into the normal processing path.
    /// </summary>
    /// <remarks>
    /// Shared by the polling loop and the startup flush, so envelopes that were already queued at
    /// signal-cli when the service started are processed rather than discarded.
    /// </remarks>
    private async Task ProcessEnvelopesAsync(IReceivedNotification[]? messages, CancellationToken cancellationToken)
    {
        if (messages is null || messages.Length == 0)
            return;

        LogEnvelopesReceived(_logger, nameof(CommunicationsBgService), messages.Length);

        var processed = 0;
        foreach (var msg in messages)
        {
            var envelopeType = msg is SignalReceivedMessage srm
                ? srm.Envelope.EnvelopeType
                : "unknown";
            LogEnvelopeDetail(_logger, nameof(CommunicationsBgService), envelopeType, msg.HasContent, msg.GroupId, msg.Sender);

            // Only process content messages from the configured group,
            // skipping our own echoes to avoid infinite reply loops.
            if (msg.HasContent
                && NormalizeGroupId(msg.GroupId) == NormalizeGroupId(_groupId)
                && msg.Sender != _signalCliConfig.PhoneNumber)
            {
                // Check for poll vote messages and route to the poll tracker
                // instead of the normal data message pipeline.
                if (msg is SignalReceivedMessage signalMsg
                    && await TryProcessPollVoteAsync(signalMsg))
                {
                    processed++;
                    continue;
                }

                await ProcessDataMessageAsync(msg, cancellationToken);
                processed++;
            }
        }

        if (processed > 0)
            LogEnvelopesProcessed(_logger, nameof(CommunicationsBgService), processed, messages.Length);
        else
            LogEnvelopesDiscarded(_logger, nameof(CommunicationsBgService), messages.Length);
    }

    private async Task ProcessDataMessageAsync(IReceivedNotification notification, CancellationToken cancellationToken)
    {
        // Detect content-only messages with no text (e.g. unrecognized poll votes) and log
        // raw extension data for diagnostics instead of confusing the agent.
        if (string.IsNullOrEmpty(notification.Message)
            && notification is SignalReceivedMessage srm
            && srm.Envelope.DataMessage is { } dm
            && dm.Attachments is null or { Length: 0 })
        {
            var extensionKeys = dm.ExtensionData?.Keys is { } keys ? string.Join(", ", keys) : "none";
            LogContentOnlyNoText(_logger, nameof(CommunicationsBgService), notification.Sender, extensionKeys,
                dm.ExtensionData is not null ? dm.ExtensionData.ToJson() : "(empty)");
            return;
        }

        LogInboundMessage(_logger, nameof(CommunicationsBgService), notification.Sender, notification.Message ?? "(attachment only)");

        // Reserve the message identity before any side effect so a redelivered envelope does not
        // repeat the agent turn, the reply or the attachment deletion.
        var identity = TryBuildIdentity(notification);
        if (identity is not null && !await _deduplicator.TryClaimAsync(identity, cancellationToken))
        {
            LogDuplicateSuppressed(_logger, nameof(CommunicationsBgService), notification.Sender);
            return;
        }

        var attachmentIds = CollectAttachmentIds(notification);
        var agentAvailable = _agent is not null && _commsAgent is not null && _provider is not null;

        // Acknowledge before the attachment is fetched: downloading and transcribing a voice note
        // takes seconds, and the sender should see that it was heard immediately. The hourglass
        // replaces this once the prompt reaches the agent.
        if (notification.Timestamp is not null && agentAvailable)
        {
            var listening = CarriesAudio(notification)
                && _speechToTextConfig.Mode is not VoiceProcessingMode.Disabled;
            await _notifier.SendProgressUpdateAsync(
                _signalCliConfig.PhoneNumber, _groupId!, listening ? "\U0001F442" : "\U0001F440",
                notification.Sender, notification.Timestamp.Value, cancellationToken);
        }

        byte[]? binaryContent = null;
        string? mimeType = null;
        var voiceSuppressed = false;
        VoiceTranscriptionResult? voice = null;
        AttachmentCleanupResult? cleanup = null;
        try
        {
            if (agentAvailable && attachmentIds.Count > 0)
            {
                var acquisition = await AcquireAttachmentAsync(notification, cancellationToken);
                (binaryContent, mimeType, voiceSuppressed, voice) =
                    (acquisition.Content, acquisition.MimeType, acquisition.VoiceSuppressed, acquisition.Voice);
            }
        }
        finally
        {
            // signal-cli keeps every received attachment on disk until it is deleted, so all
            // identifiers on the envelope are removed, including any that were not selected.
            if (attachmentIds.Count > 0)
                cleanup = await _attachmentCleaner.DeleteAllAsync(attachmentIds, cancellationToken);
        }

        if (cleanup is { Complete: false })
        {
            // The reservation is deliberately retained: reprocessing would not remove the residue
            // and would repeat the agent turn. Operator cleanup is required before promotion.
            LogAttachmentCleanupFailed(_logger, nameof(CommunicationsBgService), cleanup.Remaining.Count);
            await SendAttachmentCleanupFailureReplyAsync(cancellationToken);
            await SendFailureReactionAsync(notification, cancellationToken);
            return;
        }

        if (!agentAvailable)
        {
            LogNoAgentSkipping(_logger, nameof(CommunicationsBgService));
            await SendFailureReactionAsync(notification, cancellationToken);
            return;
        }

        var prompt = notification.Message ?? _commsAgent!.Prompt;

        // A successful transcript replaces the prompt; the audio itself is never forwarded.
        if (voice is { TranscriptAvailable: true, Text: { } transcript })
        {
            prompt = transcript;
            if (_commsAgentConfig.EchoTranscriptToDebugChat)
                await _debugNotifier.SendVoiceTranscriptDebugAsync(voice, cancellationToken);
        }

        // A voice message the pipeline could not transcribe gets one concise reply and no agent
        // turn, so nothing is persisted to the conversation.
        if (voice?.Outcome is not null and not VoiceTranscriptionOutcome.Success
            && _speechToTextConfig.Mode is VoiceProcessingMode.Enabled)
        {
            await SendVoiceFailureReplyAsync(cancellationToken);
            await SendFailureReactionAsync(notification, cancellationToken);
            return;
        }

        // ── Slash-command handling ────────────────────────────────────────────
        if (ChatCommandParser.TryParseCommand(prompt, out var chatCmd, out var cmdArg))
        {
            LogSlashCommand(_logger, nameof(CommunicationsBgService), chatCmd, notification.Sender);

            // SessionBypass needs special handling — enqueue the prompt and skip reply.
            if (chatCmd is ChatCommand.SessionBypass && !string.IsNullOrWhiteSpace(cmdArg))
            {
                await EnqueueReplyAsync(cmdArg, bypassSession: true, cancellationToken: cancellationToken);
                return;
            }

            var commandResponse = await _commandHandler.HandleCommandAsync(
                chatCmd, cmdArg, _agent!, _commsAgent!.Name,
                onModelChanged: UpdateSignalProfileNameAsync);
            if (!string.IsNullOrWhiteSpace(commandResponse))
            {
                var reply = new SignalMessageRequest
                {
                    Message = commandResponse,
                    Number = _signalCliConfig.PhoneNumber,
                    Recipients = [_groupId!]
                };
                await _notifier.SendAsync(reply, cancellationToken);
            }

            // Green tick reaction to indicate the command has been seen and processed.
            if (notification.Timestamp is not null)
                await _notifier.SendProgressUpdateAsync(
                    _signalCliConfig.PhoneNumber, _groupId!, "\u2705", notification.Sender, notification.Timestamp.Value, cancellationToken);

            return;
        }

        // A voice message that the configured mode stops short of an agent turn ends here; it has
        // already been acquired and cleaned up.
        if (voiceSuppressed && string.IsNullOrWhiteSpace(notification.Message))
        {
            LogVoiceTurnSuppressed(_logger, nameof(CommunicationsBgService), _speechToTextConfig.Mode.ToString());
            return;
        }

        await EnqueueReplyAsync(prompt, binaryContent, mimeType, sender: notification.Sender,
            timestamp: notification.Timestamp, bypassSession: false,
            inboundWasVoice: voice is not null, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Downloads the deterministically selected attachment, applying the configured
    /// <see cref="VoiceProcessingMode"/> to audio payloads.
    /// </summary>
    /// <returns>
    /// The downloaded bytes and media type, plus whether a voice payload was withheld from the
    /// agent turn by the configured mode.
    /// </returns>
    private async Task<AttachmentAcquisition> AcquireAttachmentAsync(
        IReceivedNotification notification, CancellationToken cancellationToken)
    {
        var attachments = notification.Attachments!;
        if (attachments.Count > 1)
            LogMultipleAttachments(_logger, nameof(CommunicationsBgService), attachments.Count);

        // Deterministic selection: the first attachment carrying an identifier.
        var attachment = attachments.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Id));
        if (attachment is null)
            return new(null, null, false, null);

        var isVoice = IsAudio(attachment.ContentType);
        if (isVoice && _speechToTextConfig.Mode is VoiceProcessingMode.Disabled)
        {
            LogVoiceRejected(_logger, nameof(CommunicationsBgService));
            return new(null, null, true, null);
        }

        var content = await _notifier.GetAttachmentAsync(attachment.Id!, cancellationToken);
        if (content is not null)
            LogAttachmentDownloaded(_logger, nameof(CommunicationsBgService), attachment.Id!, attachment.ContentType, content.Length);

        if (!isVoice)
            return new(content, attachment.ContentType, false, null);

        if (content is null)
            return new(null, null, true, VoiceTranscriptionResult.Failure(VoiceTranscriptionOutcome.Invalid));

        // Raw audio never reaches the agent: it is replaced by the normalised transcript, or the
        // turn is abandoned. Shadow transcribes for measurement but stops short of the agent.
        var result = await _transcriptionSvc.TranscribeAsync(content, attachment.ContentType!, cancellationToken);
        LogVoiceTranscription(_logger, nameof(CommunicationsBgService), result.Outcome.ToString(),
            result.Text?.Length ?? 0);

        //Shadow measures the pipeline but must not reach the agent, so the transcript is dropped here.
        if (_speechToTextConfig.Mode is VoiceProcessingMode.Shadow)
            return new(null, null, true, result with { Text = null });

        return new(null, null, !result.TranscriptAvailable, result);
    }

    /// <summary>The outcome of acquiring, and where applicable transcribing, one attachment.</summary>
    /// <param name="Content">Non-audio attachment bytes passed to the agent, or <see langword="null"/>.</param>
    /// <param name="MimeType">The media type of <paramref name="Content"/>.</param>
    /// <param name="VoiceSuppressed">Whether a voice payload was withheld from the agent turn.</param>
    /// <param name="Voice">The transcription result, or <see langword="null"/> for non-audio.</param>
    private sealed record AttachmentAcquisition(byte[]? Content, string? MimeType, bool VoiceSuppressed,
        VoiceTranscriptionResult? Voice);

    /// <summary>Sends the single generic failure reply used when a voice message could not be transcribed.</summary>
    /// <remarks>Carries no transcript, no audio and no identifier.</remarks>
    private async Task SendVoiceFailureReplyAsync(CancellationToken cancellationToken)
    {
        if (_groupId is null)
            return;
        var reply = new SignalMessageRequest
        {
            Message = "\U0001F507 Sorry, I could not understand that voice message.",
            Number = _signalCliConfig.PhoneNumber,
            Recipients = [_groupId]
        };
        await _notifier.SendAsync(reply, cancellationToken);
    }

    /// <summary>Sends the single generic failure reply used when attachment cleanup could not complete.</summary>
    /// <remarks>Carries no message content and no attachment identifier.</remarks>
    private async Task SendAttachmentCleanupFailureReplyAsync(CancellationToken cancellationToken)
    {
        if (_groupId is null)
            return;
        var reply = new SignalMessageRequest
        {
            Message = "\u26A0\uFE0F Sorry, I could not process that message.",
            Number = _signalCliConfig.PhoneNumber,
            Recipients = [_groupId]
        };
        await _notifier.SendAsync(reply, cancellationToken);
    }

    /// <summary>Marks the sender's message as failed, matching the agent path's red cross.</summary>
    /// <remarks>
    /// Every abandoned turn has to reach this, otherwise the message keeps its acknowledgement
    /// reaction and looks like it is still being worked on.
    /// </remarks>
    private async Task SendFailureReactionAsync(IReceivedNotification notification, CancellationToken cancellationToken)
    {
        if (_groupId is null || notification.Timestamp is null)
            return;
        await _notifier.SendProgressUpdateAsync(
            _signalCliConfig.PhoneNumber, _groupId, "\u274C", notification.Sender, notification.Timestamp.Value, cancellationToken);
    }

    /// <summary>Collects every attachment identifier carried by the envelope, selected or not.</summary>
    private static List<string> CollectAttachmentIds(IReceivedNotification notification) =>
        notification.Attachments is { Count: > 0 } attachments
            ? [.. attachments.Where(a => !string.IsNullOrWhiteSpace(a.Id)).Select(a => a.Id!)]
            : [];

    /// <summary>Builds the duplicate-suppression identity, or <see langword="null"/> when the envelope carries no timestamp.</summary>
    private SignalMessageIdentity? TryBuildIdentity(IReceivedNotification notification)
    {
        if (notification.Timestamp is not { } timestamp)
            return null;
        var account = notification is SignalReceivedMessage { Account: { Length: > 0 } accountNumber }
            ? accountNumber
            : _signalCliConfig.PhoneNumber;
        return new SignalMessageIdentity
        {
            Account = account,
            Conversation = notification.GroupId ?? notification.Sender,
            Sender = notification.Sender,
            Timestamp = timestamp,
        };
    }

    private static bool IsAudio(string? contentType) =>
        contentType?.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) == true;

    //Read from the envelope so the sender can be acknowledged before anything is downloaded.
    private static bool CarriesAudio(IReceivedNotification notification) =>
        notification.Attachments?.Any(a => IsAudio(a.ContentType)) == true;

    /// <summary>
    /// Checks whether the received message is a poll vote update and, if so, records the
    /// vote in the <see cref="IPollTracker"/> and enqueues a prompt so the agent can act
    /// on the result.
    /// </summary>
    /// <returns><see langword="true"/> if the message was a poll vote and was handled; otherwise <see langword="false"/>.</returns>
    private async Task<bool> TryProcessPollVoteAsync(SignalReceivedMessage signalMsg)
    {
        var pollUpdate = signalMsg.Envelope.DataMessage?.PollVote;
        if (pollUpdate?.TargetSentTimestamp is null || pollUpdate.OptionIndexes is null or { Length: 0 })
            return false;

        var pollId = pollUpdate.TargetSentTimestamp.Value.ToString();
        var selectedIndices = pollUpdate.OptionIndexes;
        var voter = signalMsg.Envelope.Source ?? signalMsg.Envelope.SourceNumber ?? "unknown";

        LogPollVoteReceived(_logger, nameof(CommunicationsBgService), voter, pollId, string.Join(", ", selectedIndices));

        // Fetch the poll first so we have its metadata even if it expires between
        // RecordVote and building the prompt.
        var poll = _pollTracker.GetPoll(pollId);

        if (!_pollTracker.RecordVote(pollId, voter, selectedIndices))
        {
            LogPollNotTracked(_logger, nameof(CommunicationsBgService), pollId);
            return false;
        }

        if (poll is null)
            return true;

        // Build a descriptive prompt so the agent knows the poll result.
        var voterName = signalMsg.Envelope.SourceName ?? voter;
        var selectedLabels = selectedIndices
            .Where(i => i >= 0 && i < poll.Answers.Length)
            .Select(i => poll.Answers[i]);

        var prompt = $"[POLL VOTE] {voterName} chose \"{string.Join(", ", selectedLabels)}\" "
            + $"on poll \"{poll.Question}\" (ID: {poll.PollId}). "
            + "INSTRUCTIONS: 1) Call close_poll with the ID above. 2) Act on the chosen option. 3) Do NOT present more choices unless they are in a new poll.";

        await EnqueueReplyAsync(prompt, bypassSession: false);
        return true;
    }

    /// <summary>Queues a reply for sequential processing, waiting for capacity when the queue is full.</summary>
    /// <remarks>
    /// The wait is deliberate backpressure: once a prompt has been accepted it must not be evicted
    /// by a later producer.
    /// </remarks>
    private async Task EnqueueReplyAsync(string prompt, byte[]? binaryContent = null, string? mimeType = null,
        string? sender = null, long? timestamp = null, bool bypassSession = false, string[]? extraBase64Attachments = null,
        bool inboundWasVoice = false, CancellationToken cancellationToken = default)
    {
        await _replyChannel.Writer.WriteAsync(
            new ReplyRequest(prompt, binaryContent, mimeType, sender, timestamp, bypassSession,
                extraBase64Attachments, inboundWasVoice),
            cancellationToken);
        LogReplyEnqueued(_logger, nameof(CommunicationsBgService));
    }

    private async Task DrainReplyQueueAsync(CancellationToken cancellationToken)
    {
        LogReplyQueueStarted(_logger, nameof(CommunicationsBgService));

        await foreach (var request in _replyChannel.Reader.ReadAllAsync(cancellationToken))
        {
            LogProcessingReply(_logger, nameof(CommunicationsBgService));

            try
            {
                if (request.Sender is not null)
                    await _notifier.StartProcessingAsync(_signalCliConfig.PhoneNumber, _groupId!, cancellationToken);

                // Hourglass reaction to indicate processing has started.
                if (request.Sender is not null && request.Timestamp is not null)
                    await _notifier.SendProgressUpdateAsync(
                        _signalCliConfig.PhoneNumber, _groupId!, "\u23F3", request.Sender, request.Timestamp.Value, cancellationToken);

                var (agentResult, debugSteps) = await RunAgentAsync(request.Prompt, request.BinaryContent,
                    request.MimeType, request.BypassSession, request.Sender, request.Timestamp, cancellationToken);

                if (request.Sender is not null)
                    await _notifier.StopProcessingAsync(_signalCliConfig.PhoneNumber, _groupId!, cancellationToken);

                var agentResponse = agentResult?.OutputText;
                if (!string.IsNullOrWhiteSpace(agentResponse))
                {
                    var messageWithStats = agentResponse + await _debugNotifier.FormatStatsFooterAsync(agentResult!);

                    // Convert any image attachments from tool results into Signal base64 attachments.
                    var base64Attachments = agentResult!.Attachments.Count > 0
                        ? agentResult.Attachments
                            .Select(a => $"data:{a.MimeType};filename={a.FileName ?? "photo"};base64,{a.Base64Content}")
                            .ToArray()
                        : null;

                    // Merge pre-built media attachments (e.g. from the media analysis pipeline).
                    if (request.ExtraBase64Attachments is { Length: > 0 })
                        base64Attachments = [.. base64Attachments ?? [], .. request.ExtraBase64Attachments];

                    // The agent's answer is spoken, never the diagnostic footer appended above.
                    var spoken = await _voiceReplySvc.TrySynthesizeAsync(
                        agentResponse, request.InboundWasVoice, cancellationToken);
                    if (spoken is not null)
                    {
                        var dataUri = $"data:{spoken.MediaType};filename={spoken.FileName};base64," +
                            Convert.ToBase64String(spoken.Audio.Span);
                        base64Attachments = [.. base64Attachments ?? [], dataUri];
                    }

                    var reply = new SignalMessageRequest
                    {
                        Message = messageWithStats,
                        Number = _signalCliConfig.PhoneNumber,
                        Recipients = [_groupId!],
                        Base64Attachments = base64Attachments,
                    };

                    LogSendingAgentResponse(_logger, nameof(CommunicationsBgService), messageWithStats.Length, base64Attachments?.Length ?? 0, _groupId);
                    var sendResult = await _notifier.SendAsync(reply, cancellationToken);
                    if (sendResult is not null)
                        LogMessageSent(_logger, nameof(CommunicationsBgService), sendResult.Timestamp);
                    else
                        LogSendReturnedNull(_logger, nameof(CommunicationsBgService));

                    // Send detailed debug stats to the debug phone number ("Note to Self").
                    LogDebugStats(_logger, nameof(CommunicationsBgService),
                        agentResult!.Usage is not null,
                        agentResult.Usage?.InputTokenCount,
                        agentResult.Usage?.OutputTokenCount,
                        debugSteps.Count,
                        debugSteps.Count(s => s.Result is not null),
                        debugSteps.Count(s => s.Result?.Usage is not null));
                    await _debugNotifier.SendDebugStatsAsync(request.Prompt, agentResult!, debugSteps,
                        request.BinaryContent, request.MimeType, request.Timestamp, cancellationToken);

                    // Green tick reaction to indicate successful processing.
                    if (request.Sender is not null && request.Timestamp is not null)
                        await _notifier.SendProgressUpdateAsync(
                            _signalCliConfig.PhoneNumber, _groupId!, "\u2705", request.Sender, request.Timestamp.Value, cancellationToken);
                }
                else
                {
                    LogAgentEmptyResponse(_logger, nameof(CommunicationsBgService));

                    // Red cross reaction: the agent failed (e.g. remote inference error) or
                    // produced no usable response, so do not signal success to the user.
                    if (request.Sender is not null && request.Timestamp is not null)
                        await _notifier.SendProgressUpdateAsync(
                            _signalCliConfig.PhoneNumber, _groupId!, "\u274C", request.Sender, request.Timestamp.Value, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
            {
                LogReplyProcessingError(_logger, ex, nameof(CommunicationsBgService), ex.GetType().Name, ex.Message);

                // Red cross reaction to indicate a processing failure.
                if (request.Sender is not null && request.Timestamp is not null)
                    await _notifier.SendProgressUpdateAsync(
                        _signalCliConfig.PhoneNumber, _groupId!, "\u274C", request.Sender, request.Timestamp.Value, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Normalizes a Signal group identifier to its raw base64 form.
    /// The groups list endpoint returns the raw key while the receive endpoint
    /// may prefix and double-encode it as <c>group.{Base64(rawKey)}</c>.
    /// </summary>
    private static string? NormalizeGroupId(string? groupId)
    {
        if (groupId is null)
            return null;
        if (groupId.StartsWith("group.", StringComparison.Ordinal))
        {
            var encoded = groupId["group.".Length..];
            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        return groupId;
    }

    /// <summary>
    /// Captures the parameters for a single agent reply so it can be queued and
    /// processed sequentially by <see cref="DrainReplyQueueAsync"/>.
    /// </summary>
    private sealed record ReplyRequest(
        string Prompt,
        byte[]? BinaryContent,
        string? MimeType,
        string? Sender,
        long? Timestamp,
        bool BypassSession = false,
        string[]? ExtraBase64Attachments = null,
        bool InboundWasVoice = false);
}
