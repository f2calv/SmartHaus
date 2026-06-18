using StackExchange.Redis;

namespace CasCap.Services;

public sealed partial class CommunicationsBgService
{
    private async Task EnsureConsumerGroupAsync()
    {
        try
        {
            await _db.StreamCreateConsumerGroupAsync(
                _commsAgentConfig.StreamKey,
                _commsAgentConfig.ConsumerGroup,
                _commsAgentConfig.ConsumerGroupStartId,
                createStream: true);
            LogConsumerGroupCreated(_logger, nameof(CommunicationsBgService), _commsAgentConfig.ConsumerGroup, _commsAgentConfig.StreamKey);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            LogConsumerGroupExists(_logger, nameof(CommunicationsBgService), _commsAgentConfig.ConsumerGroup);
        }
    }

    private async Task DrainStreamAsync(CancellationToken cancellationToken)
    {
        LogStreamConsuming(_logger, nameof(CommunicationsBgService), _commsAgentConfig.StreamKey,
            _commsAgentConfig.ConsumerGroup, _commsAgentConfig.ConsumerName);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var entries = await _db.StreamReadGroupAsync(
                    _commsAgentConfig.StreamKey,
                    _commsAgentConfig.ConsumerGroup,
                    _commsAgentConfig.ConsumerName,
                    _commsAgentConfig.StreamReadPosition,
                    count: _commsAgentConfig.StreamReadCount);

                if (entries.Length > 0)
                {
                    foreach (var entry in entries)
                    {
                        var commsEvent = DeserializeStreamEntry(entry);
                        if (_commsAgentConfig.AllowedSources.Count > 0
                            && !_commsAgentConfig.AllowedSources.Contains(commsEvent.Source))
                        {
                            _logger.Log(_env.IsDevelopment() ? LogLevel.Error : LogLevel.Debug,
                                "{ClassName} skipping event from unrecognised source {Source}",
                                nameof(CommunicationsBgService), commsEvent.Source);
                            await _db.StreamAcknowledgeAsync(
                                _commsAgentConfig.StreamKey,
                                _commsAgentConfig.ConsumerGroup,
                                entry.Id);
                            continue;
                        }
                        await ProcessCommsEventAsync(commsEvent, cancellationToken);
                        await _db.StreamAcknowledgeAsync(
                            _commsAgentConfig.StreamKey,
                            _commsAgentConfig.ConsumerGroup,
                            entry.Id);
                    }
                }
                else
                    await Task.Delay(TimeSpan.FromMilliseconds(_commsAgentConfig.PollingIntervalMs), cancellationToken);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOGROUP"))
            {
                LogConsumerGroupDisappeared(_logger, nameof(CommunicationsBgService));
                await EnsureConsumerGroupAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
            {
                LogStreamReadError(_logger, ex, nameof(CommunicationsBgService));
                await Task.Delay(TimeSpan.FromMilliseconds(_commsAgentConfig.PollingIntervalMs), cancellationToken);
            }
        }
    }

    private async Task ProcessCommsEventAsync(CommsEvent commsEvent, CancellationToken cancellationToken)
    {
        // Drop stale events: a producer flood or consumer backlog can leave events queued far
        // longer than they are useful. Anything older than MaxEventAgeSeconds is acknowledged
        // but dropped rather than delivered late.
        if (_commsAgentConfig.StaleEventDroppingEnabled)
        {
            var age = _timeProvider.GetUtcNow().UtcDateTime - commsEvent.TimestampUtc;
            if (age > TimeSpan.FromSeconds(_commsAgentConfig.MaxEventAgeSeconds))
            {
                _staleSinceNotice++;
                LogStreamEventStale(_logger, nameof(CommunicationsBgService), commsEvent.Source, (long)age.TotalSeconds, _staleSinceNotice);
                await MaybeSendDropNoticeAsync(cancellationToken);
                return;
            }
        }

        // Rate-limit producer-driven stream events to prevent message floods that signal-cli
        // would otherwise drip-feed to the group over hours. Gating here (before the agent runs)
        // also avoids wasted inference on suppressed events. Interactive replies to user messages
        // flow through the reply queue and are never throttled by this gate.
        if (_streamSendThrottle is not null && !_streamSendThrottle.TryAcquire())
        {
            _rateLimitedSinceNotice++;
            LogStreamEventSuppressed(_logger, nameof(CommunicationsBgService), commsEvent.Source, _rateLimitedSinceNotice);
            await MaybeSendDropNoticeAsync(cancellationToken);
            return;
        }

        LogProcessingStreamEvent(_logger, nameof(CommunicationsBgService), commsEvent.Source, commsEvent.Message);

        // Forward the raw stream event to the debug chat for observability.
        await _debugNotifier.SendStreamEventDebugAsync(commsEvent, cancellationToken);

        // Wait until Signal group resolution completes before attempting delivery.
        await _groupResolved.Task.WaitAsync(cancellationToken);

        if (_agent is null || _commsAgent is null || _provider is null)
        {
            // No agent — forward event message directly to the notification group.
            LogNoAgentForwarding(_logger, nameof(CommunicationsBgService));
            var directMsg = new SignalMessageRequest
            {
                Message = commsEvent.Message,
                Number = _signalCliConfig.PhoneNumber,
                Recipients = [_groupId!]
            };
            var sendResult = await _notifier.SendAsync(directMsg, cancellationToken);
            if (sendResult is null)
                LogDirectForwardFailed(_logger, nameof(CommunicationsBgService));
            return;
        }

        var prompt = $"[{commsEvent.Source}] {commsEvent.Message}";
        if (commsEvent.JsonPayload is not null)
            prompt += $"\n\nJSON: {commsEvent.JsonPayload}";

        // If the event carries a Redis-cached media reference, fetch the bytes and attach them.
        string[]? extraAttachments = null;
        if (commsEvent.JsonPayload is not null)
        {
            try
            {
                var mediaRef = commsEvent.JsonPayload.FromJson<MediaReference>();
                if (mediaRef?.MediaRedisKey is { Length: > 0 })
                {
                    var mediaBytes = (byte[]?)await _db.StringGetAsync(mediaRef.MediaRedisKey);
                    if (mediaBytes is { Length: > 0 })
                    {
                        var mimeType = mediaRef.MimeType ?? "image/jpeg";
                        var fileName = mediaRef.FileName ?? "media";
                        extraAttachments = [$"data:{mimeType};filename={fileName};base64,{Convert.ToBase64String(mediaBytes)}"];
                        await _db.KeyDeleteAsync(mediaRef.MediaRedisKey, CommandFlags.FireAndForget);
                        LogMediaAttached(_logger, nameof(CommunicationsBgService), mediaBytes.Length, mediaRef.MediaRedisKey);
                    }
                    else
                        LogMediaNotFound(_logger, nameof(CommunicationsBgService), mediaRef.MediaRedisKey);
                }
            }
            catch { /* JsonPayload is not a MediaCommsPayload — that's fine, skip attachment */ }
        }

        EnqueueReply(prompt, extraBase64Attachments: extraAttachments);
    }

    private CommsEvent DeserializeStreamEntry(StreamEntry entry)
    {
        var dict = entry.Values.ToDictionary(v => v.Name.ToString(), v => v.Value.ToString());
        return new CommsEvent
        {
            Source = dict.GetValueOrDefault(nameof(CommsEvent.Source)) ?? "Unknown",
            Message = dict.GetValueOrDefault(nameof(CommsEvent.Message)) ?? string.Empty,
            Environment = dict.GetValueOrDefault(nameof(CommsEvent.Environment)) ?? _env.GetAcronym(),
            TimestampUtc = DateTime.TryParse(dict.GetValueOrDefault(nameof(CommsEvent.TimestampUtc)), out var ts)
                ? ts
                : _timeProvider.GetUtcNow().UtcDateTime,
            JsonPayload = dict.GetValueOrDefault(nameof(CommsEvent.JsonPayload)),
        };
    }

    /// <summary>
    /// Sends a single drop-notice message to the group when stream events are being dropped
    /// (rate-limited and/or stale), rate-limited to at most once per
    /// <see cref="CommsAgentConfig.DropNoticeIntervalMs"/> so the notice itself cannot flood the
    /// group. The first drop always emits a notice immediately.
    /// </summary>
    private async Task MaybeSendDropNoticeAsync(CancellationToken cancellationToken)
    {
        // The group must be resolved before we can notify; until then drops are silent (counters
        // keep accumulating so the eventual notice reports the full total).
        if (!_groupResolved.Task.IsCompletedSuccessfully || _groupId is null)
            return;

        var nowTicks = _timeProvider.GetUtcNow().UtcTicks;
        var intervalTicks = TimeSpan.FromMilliseconds(_commsAgentConfig.DropNoticeIntervalMs).Ticks;
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
            parts.Add($"{rateLimited} over the {_commsAgentConfig.StreamSendRatePerMinute}/min rate limit");
        if (stale > 0)
            parts.Add($"{stale} older than {_commsAgentConfig.MaxEventAgeSeconds}s");

        var notice = new SignalMessageRequest
        {
            Message = $"\uD83D\uDEA6 Dropped {total} notification(s) to avoid flooding the group \u2014 {string.Join(", ", parts)}.",
            Number = _signalCliConfig.PhoneNumber,
            Recipients = [_groupId],
        };

        try
        {
            var result = await _notifier.SendAsync(notice, cancellationToken);
            LogDropNoticeSent(_logger, nameof(CommunicationsBgService), total, result is not null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
        {
            LogDropNoticeFailed(_logger, ex, nameof(CommunicationsBgService), ex.GetType().Name, ex.Message);
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
