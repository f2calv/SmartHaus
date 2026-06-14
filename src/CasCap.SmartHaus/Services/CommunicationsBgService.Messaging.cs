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
                if (messages is not null && messages.Length > 0)
                {
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
                                && TryProcessPollVote(signalMsg))
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

        if (_agent is null || _commsAgent is null || _provider is null)
        {
            LogNoAgentSkipping(_logger, nameof(CommunicationsBgService));
            return;
        }

        // Download the first attachment (images, audio) for the agent; additional attachments are logged but skipped.
        byte[]? binaryContent = null;
        string? mimeType = null;
        if (notification.Attachments is { Count: > 0 })
        {
            if (notification.Attachments.Count > 1)
                LogMultipleAttachments(_logger, nameof(CommunicationsBgService), notification.Attachments.Count);

            var attachment = notification.Attachments[0];
            if (!string.IsNullOrWhiteSpace(attachment.Id))
            {
                binaryContent = await _notifier.GetAttachmentAsync(attachment.Id, cancellationToken);
                mimeType = attachment.ContentType;
                if (binaryContent is not null)
                    LogAttachmentDownloaded(_logger, nameof(CommunicationsBgService), attachment.Id, mimeType, binaryContent.Length);
            }
        }

        var prompt = notification.Message ?? _commsAgent.Prompt;

        // ── Slash-command handling ────────────────────────────────────────────
        if (ChatCommandParser.TryParseCommand(prompt, out var chatCmd, out var cmdArg))
        {
            LogSlashCommand(_logger, nameof(CommunicationsBgService), chatCmd, notification.Sender);

            // SessionBypass needs special handling — enqueue the prompt and skip reply.
            if (chatCmd is ChatCommand.SessionBypass && !string.IsNullOrWhiteSpace(cmdArg))
            {
                EnqueueReply(cmdArg, bypassSession: true);
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
                    _signalCliConfig.PhoneNumber, _groupId!, "\u2705", notification.Sender, notification.Timestamp.Value);

            return;
        }

        // Acknowledge the sender's message with an eyes reaction to confirm it has been seen.
        if (notification.Timestamp is not null)
            await _notifier.SendProgressUpdateAsync(
                _signalCliConfig.PhoneNumber, _groupId!, "\U0001F440", notification.Sender, notification.Timestamp.Value);

        EnqueueReply(prompt, binaryContent, mimeType, sender: notification.Sender, timestamp: notification.Timestamp,
            bypassSession: false);
    }

    /// <summary>
    /// Checks whether the received message is a poll vote update and, if so, records the
    /// vote in the <see cref="IPollTracker"/> and enqueues a prompt so the agent can act
    /// on the result.
    /// </summary>
    /// <returns><see langword="true"/> if the message was a poll vote and was handled; otherwise <see langword="false"/>.</returns>
    private bool TryProcessPollVote(SignalReceivedMessage signalMsg)
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

        EnqueueReply(prompt, bypassSession: false);
        return true;
    }

    private void EnqueueReply(string prompt, byte[]? binaryContent = null, string? mimeType = null,
        string? sender = null, long? timestamp = null, bool bypassSession = false, string[]? extraBase64Attachments = null)
    {
        _replyChannel.Writer.TryWrite(new ReplyRequest(prompt, binaryContent, mimeType, sender, timestamp, bypassSession, extraBase64Attachments));
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
                    await _notifier.StartProcessingAsync(_signalCliConfig.PhoneNumber, _groupId!);

                // Hourglass reaction to indicate processing has started.
                if (request.Sender is not null && request.Timestamp is not null)
                    await _notifier.SendProgressUpdateAsync(
                        _signalCliConfig.PhoneNumber, _groupId!, "\u23F3", request.Sender, request.Timestamp.Value);

                var (agentResult, debugSteps) = await RunAgentAsync(request.Prompt, request.BinaryContent,
                    request.MimeType, request.BypassSession, request.Sender, request.Timestamp, cancellationToken);

                if (request.Sender is not null)
                    await _notifier.StopProcessingAsync(_signalCliConfig.PhoneNumber, _groupId!);

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
                        request.BinaryContent, request.MimeType, cancellationToken);

                    // Green tick reaction to indicate successful processing.
                    if (request.Sender is not null && request.Timestamp is not null)
                        await _notifier.SendProgressUpdateAsync(
                            _signalCliConfig.PhoneNumber, _groupId!, "\u2705", request.Sender, request.Timestamp.Value);
                }
                else
                {
                    LogAgentEmptyResponse(_logger, nameof(CommunicationsBgService));

                    // Red cross reaction: the agent failed (e.g. remote inference error) or
                    // produced no usable response, so do not signal success to the user.
                    if (request.Sender is not null && request.Timestamp is not null)
                        await _notifier.SendProgressUpdateAsync(
                            _signalCliConfig.PhoneNumber, _groupId!, "\u274C", request.Sender, request.Timestamp.Value);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
            {
                LogReplyProcessingError(_logger, ex, nameof(CommunicationsBgService), ex.GetType().Name, ex.Message);

                // Red cross reaction to indicate a processing failure.
                if (request.Sender is not null && request.Timestamp is not null)
                    await _notifier.SendProgressUpdateAsync(
                        _signalCliConfig.PhoneNumber, _groupId!, "\u274C", request.Sender, request.Timestamp.Value);
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
        string[]? ExtraBase64Attachments = null);
}
