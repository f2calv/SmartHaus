namespace CasCap.Services;

/// <summary>Inbound subscription, voice acquisition and reply queue logic for <see cref="CommunicationsBgService"/>.</summary>
public sealed partial class CommunicationsBgService
{
    private const string Eyes = "\U0001F440";
    private const string Ear = "\U0001F442";
    private const string Hourglass = "\u23F3";
    private const string GreenTick = "\u2705";
    private const string RedCross = "\u274C";

    private async Task SubscribeToMessagesAsync(CancellationToken cancellationToken)
    {
        LogPollingStarted(logger, nameof(CommunicationsBgService));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await foreach (var message in signalizrClient
                    .SubscribeAsync(cancellationToken)
                    .ConfigureAwait(false))
                {
                    if (!ShouldProcessMessage(message))
                        continue;

                    await ProcessInboundAsync(message, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
            {
                LogPollCycleError(logger, ex, nameof(CommunicationsBgService), ex.GetType().Name, ex.Message);
            }

            // A clean end of the Signalizr gRPC subscription also means the gateway went away, so both paths back off.
            await Task.Delay(TimeSpan.FromMilliseconds(commsConfig.Value.PollingIntervalMs), timeProvider, cancellationToken);
        }
    }

    /// <summary>Accepts a delivery for the chat group that carries content and was not sent by the gateway itself.</summary>
    /// <remarks>
    /// Ignoring the gateway's own traffic relies on the account being dedicated to the gateway.
    /// On an account linked to the owner's phone, the owner's messages are marked as its own too,
    /// and this filter would then discard them.
    /// </remarks>
    internal bool ShouldProcessMessage(SignalizrMessage message) =>
        !message.FromSelf
        && (!string.IsNullOrWhiteSpace(message.Message) || message.Attachments.Count > 0 || message.PollVote is not null)
        && string.Equals(message.GroupName, commsConfig.Value.GroupName, StringComparison.Ordinal);

    private async Task ProcessInboundAsync(SignalizrMessage message, CancellationToken cancellationToken)
    {
        var active = ActiveResponder;
        if (active is null)
        {
            // Nothing can answer, so the message is only recorded; no reaction, download or reservation.
            LogInboundIgnored(logger, nameof(CommunicationsBgService), message.Message?.Length ?? 0, message.Attachments.Count);
            return;
        }

        var notification = SignalizrReceivedNotification.From(message);
        if (notification.PollVote is { } pollVote && pollVote.OptionIndexes.Count > 0)
        {
            var voteTurn = await active.HandlePollVoteAsync(notification.Sender, pollVote, cancellationToken);
            if (voteTurn is not null)
            {
                await EnqueueTurnAsync(voteTurn, cancellationToken);
                return;
            }
        }

        await ProcessDataMessageAsync(notification, active, cancellationToken);
    }

    private async Task ProcessDataMessageAsync(IReceivedNotification notification, ICommsResponder active,
        CancellationToken cancellationToken)
    {
        // A poll vote nobody tracks carries no text to act on.
        if (string.IsNullOrEmpty(notification.Message) && notification.Attachments is null or { Count: 0 })
            return;

        LogInboundMessage(logger, nameof(CommunicationsBgService), notification.Message?.Length ?? 0,
            notification.Attachments?.Count ?? 0);

        // Reserve the message identity before any side effect so a redelivered envelope does not
        // repeat the turn or the reply.
        var identity = TryBuildIdentity(notification);
        if (identity is not null && !await deduplicator.TryClaimAsync(identity, cancellationToken))
        {
            LogDuplicateSuppressed(logger, nameof(CommunicationsBgService));
            return;
        }

        // Acknowledge before the attachment is fetched: downloading and transcribing a voice note
        // takes seconds, and the sender should see that it was heard immediately. The hourglass
        // replaces this once the prompt reaches the responder.
        if (notification.Timestamp is not null)
        {
            var listening = CarriesAudio(notification)
                && speechToTextConfig.Value.Mode is not VoiceProcessingMode.Disabled;
            await SetReactionAsync(listening ? Ear : Eyes, notification.Sender, notification.Timestamp.Value, cancellationToken);
        }

        byte[]? binaryContent = null;
        string? mimeType = null;
        var voiceSuppressed = false;
        VoiceTranscriptionResult? voice = null;
        // Signalizr owns attachment retention, so nothing here deletes the downloaded copy.
        if (notification.Attachments is { Count: > 0 })
        {
            var acquisition = await AcquireAttachmentAsync(notification, cancellationToken);
            (binaryContent, mimeType, voiceSuppressed, voice) =
                (acquisition.Content, acquisition.MimeType, acquisition.VoiceSuppressed, acquisition.Voice);
        }

        var prompt = notification.Message ?? active.DefaultPrompt;

        // A successful transcript replaces the prompt; the audio itself is never forwarded.
        if (voice is { TranscriptAvailable: true, Text: { } transcript })
        {
            prompt = transcript;
            if (commsConfig.Value.EchoTranscriptToDebugChat)
                await SendVoiceTranscriptDebugAsync(voice, cancellationToken);
        }

        // A voice message the pipeline could not transcribe gets one concise reply and no turn, so
        // nothing is persisted to the conversation.
        if (voice?.Outcome is not null and not VoiceTranscriptionOutcome.Success
            && speechToTextConfig.Value.Mode is VoiceProcessingMode.Enabled)
        {
            await SendVoiceFailureReplyAsync(cancellationToken);
            await SendFailureReactionAsync(notification, cancellationToken);
            return;
        }

        var command = await active.TryHandleCommandAsync(prompt, cancellationToken);
        if (command is not null)
        {
            if (command.DeferredTurn is not null)
            {
                await EnqueueTurnAsync(command.DeferredTurn, cancellationToken);
                return;
            }

            if (!string.IsNullOrWhiteSpace(command.ReplyText))
                await SendMessageAsync(command.ReplyText, cancellationToken);

            // Green tick reaction to indicate the command has been seen and processed.
            if (notification.Timestamp is not null)
                await SetReactionAsync(GreenTick, notification.Sender, notification.Timestamp.Value, cancellationToken);
            return;
        }

        // A voice message that the configured mode stops short of a turn ends here; it has already
        // been acquired and cleaned up.
        if (voiceSuppressed && string.IsNullOrWhiteSpace(notification.Message))
        {
            LogVoiceTurnSuppressed(logger, nameof(CommunicationsBgService), speechToTextConfig.Value.Mode.ToString());
            return;
        }

        await EnqueueTurnAsync(new CommsTurn(prompt, binaryContent, mimeType, notification.Sender,
            notification.Timestamp, InboundWasVoice: voice is not null), cancellationToken);
    }

    /// <summary>
    /// Downloads the deterministically selected attachment, applying the configured
    /// <see cref="VoiceProcessingMode"/> to voice payloads.
    /// </summary>
    /// <returns>
    /// The downloaded bytes and media type, plus whether a voice payload was withheld from the
    /// turn by the configured mode.
    /// </returns>
    private async Task<AttachmentAcquisition> AcquireAttachmentAsync(
        IReceivedNotification notification, CancellationToken cancellationToken)
    {
        var attachments = notification.Attachments ?? [];
        if (attachments.Count > 1)
            LogMultipleAttachments(logger, nameof(CommunicationsBgService), attachments.Count);

        // Deterministic selection: the first attachment carrying an identifier.
        var attachment = attachments.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Id));
        var attachmentId = attachment?.Id;
        if (attachment is null || string.IsNullOrWhiteSpace(attachmentId))
            return new(null, null, false, null);

        var isVoice = IsAudio(attachment.ContentType);
        if (isVoice && speechToTextConfig.Value.Mode is VoiceProcessingMode.Disabled)
        {
            LogVoiceRejected(logger, nameof(CommunicationsBgService));
            return new(null, null, true, null);
        }

        var content = await signalizrClient.GetAttachmentAsync(attachmentId, cancellationToken);
        if (content.Length > 0)
            LogAttachmentDownloaded(logger, nameof(CommunicationsBgService), attachment.ContentType, content.Length);

        if (!isVoice)
            return new(content, attachment.ContentType, false, null);

        if (content.Length == 0)
            return new(null, null, true, VoiceTranscriptionResult.Failure(VoiceTranscriptionOutcome.Invalid));

        ArgumentNullException.ThrowIfNull(attachment.ContentType);

        // Raw audio never reaches the responder: it is replaced by the normalised transcript, or the
        // turn is abandoned. Shadow transcribes for measurement but stops short of a turn.
        var result = await transcriptionSvc.Transcribe(content, attachment.ContentType, cancellationToken);
        LogVoiceTranscription(logger, nameof(CommunicationsBgService), result.Outcome.ToString(),
            result.Text?.Length ?? 0);

        //Shadow measures the pipeline but must not reach the responder, so the transcript is dropped here.
        if (speechToTextConfig.Value.Mode is VoiceProcessingMode.Shadow)
            return new(null, null, true, result with { Text = null });

        return new(null, null, !result.TranscriptAvailable, result);
    }

    /// <summary>The outcome of acquiring, and where applicable transcribing, one attachment.</summary>
    /// <param name="Content">Non-audio attachment bytes passed to the responder, or <see langword="null"/>.</param>
    /// <param name="MimeType">The media type of <paramref name="Content"/>.</param>
    /// <param name="VoiceSuppressed">Whether a voice payload was withheld from the turn.</param>
    /// <param name="Voice">The transcription result, or <see langword="null"/> for non-audio.</param>
    private sealed record AttachmentAcquisition(byte[]? Content, string? MimeType, bool VoiceSuppressed,
        VoiceTranscriptionResult? Voice);

    /// <summary>
    /// Sends the transcript of an inbound voice message to <see cref="CommsConfig.MonitorGroupName"/>
    /// so a misheard command can be diagnosed against what the responder actually received.
    /// </summary>
    /// <remarks>
    /// Only called when <see cref="CommsConfig.EchoTranscriptToDebugChat"/> is enabled. The transcript
    /// goes to the monitor group alone and never to a log sink or telemetry.
    /// </remarks>
    private async Task SendVoiceTranscriptDebugAsync(VoiceTranscriptionResult result, CancellationToken cancellationToken)
    {
        if (commsConfig.Value.MonitorGroupName is not { Length: > 0 } monitorGroupName)
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

            await signalizrClient.SendAsync(monitorGroupName, sb.ToString(), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogTranscriptEchoFailed(logger, ex, nameof(CommunicationsBgService));
        }
    }

    //Seconds of audio processed per second of wall clock; above one means faster than playback.
    private static string Realtime(TimeSpan? audioDuration, TimeSpan elapsed) =>
        audioDuration is { } audio && elapsed > TimeSpan.Zero
            ? $" ({audio.TotalSeconds / elapsed.TotalSeconds:N1}x)"
            : string.Empty;

    /// <summary>Sends the single generic failure reply used when a voice message could not be transcribed.</summary>
    /// <remarks>Carries no transcript, no audio and no identifier.</remarks>
    private Task SendVoiceFailureReplyAsync(CancellationToken cancellationToken) =>
        SendMessageAsync("\U0001F507 Sorry, I could not understand that voice message.", cancellationToken);

    /// <summary>Marks the sender's message as failed with a red cross.</summary>
    /// <remarks>
    /// Every abandoned turn has to reach this, otherwise the message keeps its acknowledgement
    /// reaction and looks like it is still being worked on.
    /// </remarks>
    private async Task SendFailureReactionAsync(IReceivedNotification notification, CancellationToken cancellationToken)
    {
        if (notification.Timestamp is null)
            return;
        await SetReactionAsync(RedCross, notification.Sender, notification.Timestamp.Value, cancellationToken);
    }

    /// <summary>Builds the duplicate-suppression identity, or <see langword="null"/> when the envelope carries no timestamp.</summary>
    private static SignalMessageIdentity? TryBuildIdentity(IReceivedNotification notification)
    {
        if (notification.Timestamp is not { } timestamp)
            return null;
        return new SignalMessageIdentity
        {
            Account = SignalizrAccount,
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

    /// <summary>Queues a turn for sequential processing, waiting for capacity when the queue is full.</summary>
    /// <remarks>
    /// The wait is deliberate backpressure: once a turn has been accepted it must not be evicted by a
    /// later producer.
    /// </remarks>
    private async Task EnqueueTurnAsync(CommsTurn turn, CancellationToken cancellationToken)
    {
        await _replyChannel.Writer.WriteAsync(turn, cancellationToken);
        LogReplyEnqueued(logger, nameof(CommunicationsBgService));
    }

    private async Task DrainReplyQueueAsync(CancellationToken cancellationToken)
    {
        LogReplyQueueStarted(logger, nameof(CommunicationsBgService));

        await foreach (var turn in _replyChannel.Reader.ReadAllAsync(cancellationToken))
        {
            LogProcessingReply(logger, nameof(CommunicationsBgService));

            try
            {
                if (turn.Sender is not null)
                    await StartTypingAsync(cancellationToken);

                // Hourglass reaction to indicate processing has started.
                if (turn.Sender is not null && turn.Timestamp is not null)
                    await SetReactionAsync(Hourglass, turn.Sender, turn.Timestamp.Value, cancellationToken);

                //Only the responder path enqueues, so a missing responder means it was withdrawn.
                var reply = ActiveResponder is { } active ? await active.RespondAsync(turn, cancellationToken) : null;

                if (turn.Sender is not null)
                    await StopTypingAsync(cancellationToken);

                if (reply is not null && !string.IsNullOrWhiteSpace(reply.Text))
                {
                    List<string> attachments = [.. reply.Base64Attachments ?? [], .. turn.ExtraBase64Attachments ?? []];

                    // The answer is spoken, never the diagnostic footer appended below.
                    var spoken = await voiceReplySvc.TrySynthesizeAsync(reply.Text, turn.InboundWasVoice, cancellationToken);
                    if (spoken is not null)
                        attachments.Add($"data:{spoken.MediaType};filename={spoken.FileName};base64," +
                            Convert.ToBase64String(spoken.Audio.Span));

                    var message = reply.Text + reply.Footer;
                    LogSendingReply(logger, nameof(CommunicationsBgService), message.Length, attachments.Count);
                    var sendTimestamp = await SendMessageAsync(message, attachments.Count > 0 ? attachments : null, cancellationToken);
                    LogMessageSent(logger, nameof(CommunicationsBgService), sendTimestamp);

                    if (reply.AfterSendAsync is not null)
                        await reply.AfterSendAsync(cancellationToken);

                    // Green tick reaction to indicate successful processing.
                    if (turn.Sender is not null && turn.Timestamp is not null)
                        await SetReactionAsync(GreenTick, turn.Sender, turn.Timestamp.Value, cancellationToken);
                }
                else
                {
                    LogEmptyReply(logger, nameof(CommunicationsBgService));

                    // Red cross reaction: the responder failed (e.g. remote inference error) or
                    // produced no usable reply, so do not signal success to the user.
                    if (turn.Sender is not null && turn.Timestamp is not null)
                        await SetReactionAsync(RedCross, turn.Sender, turn.Timestamp.Value, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
            {
                LogReplyProcessingError(logger, ex, nameof(CommunicationsBgService), ex.GetType().Name, ex.Message);

                // Red cross reaction to indicate a processing failure.
                if (turn.Sender is not null && turn.Timestamp is not null)
                    await SetReactionAsync(RedCross, turn.Sender, turn.Timestamp.Value, cancellationToken);
            }
        }
    }

    private Task<string> SendMessageAsync(string message, CancellationToken cancellationToken) =>
        SendMessageAsync(message, base64Attachments: null, cancellationToken);

    private Task<string> SendMessageAsync(
        string message,
        IReadOnlyList<string>? base64Attachments,
        CancellationToken cancellationToken) =>
        signalizrClient.SendAsync(commsConfig.Value.GroupName, message, base64Attachments, cancellationToken);

    /// <summary>Sets a progress reaction on a message in the chat group, best effort.</summary>
    /// <remarks>A reaction is feedback about the work, and failing to show it must not abandon the work itself.</remarks>
    private Task SetReactionAsync(string reaction, string sender, long timestamp, CancellationToken cancellationToken) =>
        signalizrClient.TrySetReactionAsync(commsConfig.Value.GroupName, reaction, timestamp, sender,
            ex => LogGroupInteractionFailed(logger, ex, nameof(CommunicationsBgService), "reaction"), cancellationToken);

    /// <summary>Shows the typing indicator in the chat group, best effort.</summary>
    private Task StartTypingAsync(CancellationToken cancellationToken) =>
        signalizrClient.TryStartTypingAsync(commsConfig.Value.GroupName,
            ex => LogGroupInteractionFailed(logger, ex, nameof(CommunicationsBgService), "typing"), cancellationToken);

    /// <summary>Clears the typing indicator in the chat group, best effort.</summary>
    private Task StopTypingAsync(CancellationToken cancellationToken) =>
        signalizrClient.TryStopTypingAsync(commsConfig.Value.GroupName,
            ex => LogGroupInteractionFailed(logger, ex, nameof(CommunicationsBgService), "typing"), cancellationToken);
}
