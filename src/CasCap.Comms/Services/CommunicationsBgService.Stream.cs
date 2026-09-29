namespace CasCap.Services;

/// <summary>Redis Stream consumption logic for <see cref="CommunicationsBgService"/>.</summary>
public sealed partial class CommunicationsBgService
{
    //Stream timestamps are written as round-trip UTC; parsing without these styles converts them to local time.
    private const DateTimeStyles UtcTimestampStyles = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;

    //Used only when a producer's MediaReference omits the media type.
    private const string FallbackMediaType = "application/octet-stream";

    private async Task EnsureConsumerGroupAsync()
    {
        try
        {
            await Db.StreamCreateConsumerGroupAsync(
                commsConfig.Value.StreamKey,
                commsConfig.Value.ConsumerGroup,
                commsConfig.Value.ConsumerGroupStartId,
                createStream: true);
            LogConsumerGroupCreated(logger, nameof(CommunicationsBgService), commsConfig.Value.ConsumerGroup, commsConfig.Value.StreamKey);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            LogConsumerGroupExists(logger, nameof(CommunicationsBgService), commsConfig.Value.ConsumerGroup);
        }
    }

    private async Task DrainStreamAsync(CancellationToken cancellationToken)
    {
        LogStreamConsuming(logger, nameof(CommunicationsBgService), commsConfig.Value.StreamKey,
            commsConfig.Value.ConsumerGroup, commsConfig.Value.ConsumerName);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var entries = await Db.StreamReadGroupAsync(
                    commsConfig.Value.StreamKey,
                    commsConfig.Value.ConsumerGroup,
                    commsConfig.Value.ConsumerName,
                    commsConfig.Value.StreamReadPosition,
                    count: commsConfig.Value.StreamReadCount);

                if (entries.Length > 0)
                {
                    foreach (var entry in entries)
                    {
                        var commsEvent = DeserializeStreamEntry(entry);
                        if (commsConfig.Value.AllowedSources.Count > 0
                            && !commsConfig.Value.AllowedSources.Contains(commsEvent.Source))
                        {
                            logger.Log(env.IsDevelopment() ? LogLevel.Error : LogLevel.Debug,
                                "{ClassName} skipping event from unrecognised source {Source}",
                                nameof(CommunicationsBgService), commsEvent.Source);
                            await Db.StreamAcknowledgeAsync(
                                commsConfig.Value.StreamKey,
                                commsConfig.Value.ConsumerGroup,
                                entry.Id);
                            continue;
                        }
                        await ProcessCommsEventAsync(commsEvent, cancellationToken);
                        await Db.StreamAcknowledgeAsync(
                            commsConfig.Value.StreamKey,
                            commsConfig.Value.ConsumerGroup,
                            entry.Id);
                    }
                }
                else
                    await Task.Delay(TimeSpan.FromMilliseconds(commsConfig.Value.PollingIntervalMs), timeProvider, cancellationToken);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOGROUP"))
            {
                LogConsumerGroupDisappeared(logger, nameof(CommunicationsBgService));
                await EnsureConsumerGroupAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
            {
                LogStreamReadError(logger, ex, nameof(CommunicationsBgService));
                await Task.Delay(TimeSpan.FromMilliseconds(commsConfig.Value.PollingIntervalMs), timeProvider, cancellationToken);
            }
        }
    }

    private async Task ProcessCommsEventAsync(CommsEvent commsEvent, CancellationToken cancellationToken)
    {
        // Drop stale events: a producer flood or consumer backlog can leave events queued far
        // longer than they are useful. Anything older than MaxEventAgeMs is acknowledged
        // but dropped rather than delivered late.
        if (commsConfig.Value.StaleEventDroppingEnabled)
        {
            var age = timeProvider.GetUtcNow().UtcDateTime - commsEvent.TimestampUtc;
            if (age > TimeSpan.FromMilliseconds(commsConfig.Value.MaxEventAgeMs))
            {
                _staleSinceNotice++;
                LogStreamEventStale(logger, nameof(CommunicationsBgService), commsEvent.Source, (long)age.TotalSeconds, _staleSinceNotice);
                await MaybeSendDropNoticeAsync(cancellationToken);
                return;
            }
        }

        // Rate-limit producer-driven stream events to prevent message floods that the gateway
        // would otherwise drip-feed to the group over hours. Gating here (before any responder runs)
        // also avoids wasted inference on suppressed events. Interactive replies to user messages
        // flow through the reply queue and are never throttled by this gate.
        if (_streamSendThrottle is not null && !_streamSendThrottle.TryAcquire())
        {
            _rateLimitedSinceNotice++;
            LogStreamEventSuppressed(logger, nameof(CommunicationsBgService), commsEvent.Source, _rateLimitedSinceNotice);
            await MaybeSendDropNoticeAsync(cancellationToken);
            return;
        }

        LogProcessingStreamEvent(logger, nameof(CommunicationsBgService), commsEvent.Source, commsEvent.Message);

        // Wait until the gateway serves the chat group before attempting delivery.
        await _groupReady.Task.WaitAsync(cancellationToken);

        var group = groupRouter.ResolveGroup(commsEvent);
        var attachments = await TryFetchMediaAttachmentAsync(commsEvent);

        // Events routed away from the chat group are operational diagnostics, not prompts, so they are
        // delivered directly; so is everything when no responder is available or stream turns are off.
        var active = commsConfig.Value.StreamEventTurnsEnabled ? ActiveResponder : null;
        if (active is null || !string.Equals(group, commsConfig.Value.GroupName, StringComparison.Ordinal))
        {
            var timestamp = await signalizrClient.SendAsync(group, eventFormatter.Format(commsEvent), attachments, cancellationToken);
            LogStreamEventSent(logger, nameof(CommunicationsBgService), commsEvent.Source, timestamp);
            return;
        }

        var turn = await active.CreateStreamTurnAsync(commsEvent, attachments, cancellationToken);
        await EnqueueTurnAsync(turn, cancellationToken);
    }

    /// <summary>Fetches the Redis-cached media an event references, deleting the key once read.</summary>
    /// <returns>The signal-cli data-URI attachment, or <see langword="null"/> when the event carries no media.</returns>
    private async Task<string[]?> TryFetchMediaAttachmentAsync(CommsEvent commsEvent)
    {
        if (commsEvent.JsonPayload is null)
            return null;
        try
        {
            var mediaRef = JsonSerializer.Deserialize<MediaReference>(commsEvent.JsonPayload);
            if (mediaRef?.MediaRedisKey is not { Length: > 0 } mediaKey)
                return null;

            var mediaBytes = (byte[]?)await Db.StringGetAsync(mediaKey);
            if (mediaBytes is not { Length: > 0 })
            {
                LogMediaNotFound(logger, nameof(CommunicationsBgService), mediaKey);
                return null;
            }

            var mimeType = mediaRef.MimeType ?? FallbackMediaType;
            var fileName = mediaRef.FileName ?? "media";
            await Db.KeyDeleteAsync(mediaKey, CommandFlags.FireAndForget);
            LogMediaAttached(logger, nameof(CommunicationsBgService), mediaBytes.Length, mediaKey);
            return [$"data:{mimeType};filename={fileName};base64,{Convert.ToBase64String(mediaBytes)}"];
        }
        catch (JsonException)
        {
            // The payload is not a media reference, so the event is sent without an attachment.
            return null;
        }
        catch (RedisException ex)
        {
            LogMediaFetchFailed(logger, ex, nameof(CommunicationsBgService), ex.GetType().Name);
            return null;
        }
    }

    private CommsEvent DeserializeStreamEntry(StreamEntry entry)
    {
        var dict = entry.Values.ToDictionary(v => v.Name.ToString(), v => v.Value.ToString());
        return new CommsEvent
        {
            Source = dict.GetValueOrDefault(nameof(CommsEvent.Source)) ?? "Unknown",
            Message = dict.GetValueOrDefault(nameof(CommsEvent.Message)) ?? string.Empty,
            Environment = dict.GetValueOrDefault(nameof(CommsEvent.Environment)) ?? env.GetAcronym(),
            TimestampUtc = DateTime.TryParse(dict.GetValueOrDefault(nameof(CommsEvent.TimestampUtc)), CultureInfo.InvariantCulture,
                UtcTimestampStyles, out var ts)
                ? ts
                : timeProvider.GetUtcNow().UtcDateTime,
            JsonPayload = dict.GetValueOrDefault(nameof(CommsEvent.JsonPayload)),
        };
    }

    /// <summary>
    /// Sends a single drop-notice message to the monitor group, or the chat group when none is
    /// configured, when stream events are being dropped (rate-limited and/or stale).
    /// </summary>
    /// <remarks>
    /// Rate-limited to at most once per <see cref="CommsConfig.DropNoticeIntervalMs"/> so the
    /// notice itself cannot flood the group. The first drop always emits a notice immediately.
    /// </remarks>
    private async Task MaybeSendDropNoticeAsync(CancellationToken cancellationToken)
    {
        // The group must be ready before we can notify; until then drops are silent (counters
        // keep accumulating so the eventual notice reports the full total).
        if (!_groupReady.Task.IsCompletedSuccessfully)
            return;

        var nowTicks = timeProvider.GetUtcNow().UtcTicks;
        var intervalTicks = TimeSpan.FromMilliseconds(commsConfig.Value.DropNoticeIntervalMs).Ticks;
        if (_lastDropNoticeTicks != 0 && nowTicks - _lastDropNoticeTicks < intervalTicks)
            return;
        _lastDropNoticeTicks = nowTicks;

        var rateLimited = _rateLimitedSinceNotice;
        var stale = _staleSinceNotice;
        _rateLimitedSinceNotice = 0;
        _staleSinceNotice = 0;

        var total = rateLimited + stale;
        if (total == 0)
            return;

        var parts = new List<string>(2);
        if (rateLimited > 0)
            parts.Add($"{rateLimited} over the {commsConfig.Value.StreamSendRatePerMinute}/min rate limit");
        if (stale > 0)
            parts.Add($"{stale} older than {commsConfig.Value.MaxEventAgeMs}ms");

        var notice = $"\uD83D\uDEA6 Dropped {total} notification(s) to avoid flooding the group \u2014 {string.Join(", ", parts)}.";

        try
        {
            // Drop notices are operator diagnostics, so they go to the monitor group when one is configured.
            var group = commsConfig.Value.MonitorGroupName is { Length: > 0 } monitorGroupName
                ? monitorGroupName
                : commsConfig.Value.GroupName;
            _ = await signalizrClient.SendAsync(group, notice, cancellationToken);
            LogDropNoticeSent(logger, nameof(CommunicationsBgService), total, true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
        {
            LogDropNoticeFailed(logger, ex, nameof(CommunicationsBgService), ex.GetType().Name, ex.Message);
        }
    }

    /// <summary>
    /// Token-bucket throttle gating producer-driven stream-event forwarding. Replenishes at a
    /// fixed rate up to a fixed capacity (burst). Guarded with a lock for safety even though the
    /// stream drain loop is the only caller.
    /// </summary>
    private sealed class StreamSendThrottle(int capacity, double refillPerSecond, TimeProvider timeProvider)
    {
        private readonly Lock _lock = new();
        private double _tokens = capacity;
        private long _lastRefillTimestamp = timeProvider.GetTimestamp();

        /// <summary>Attempts to consume a single token, replenishing first based on elapsed time.</summary>
        /// <returns><see langword="true"/> if a token was available and consumed; otherwise <see langword="false"/>.</returns>
        public bool TryAcquire()
        {
            lock (_lock)
            {
                var now = timeProvider.GetTimestamp();
                var elapsed = timeProvider.GetElapsedTime(_lastRefillTimestamp, now).TotalSeconds;
                _lastRefillTimestamp = now;
                _tokens = Math.Min(capacity, _tokens + (elapsed * refillPerSecond));
                if (_tokens >= 1d)
                {
                    _tokens -= 1d;
                    return true;
                }
                return false;
            }
        }
    }
}
