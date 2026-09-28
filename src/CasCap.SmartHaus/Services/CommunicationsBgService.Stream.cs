using StackExchange.Redis;

namespace CasCap.Services;

public sealed partial class CommunicationsBgService
{
    /// <summary>The Redis database backing the comms stream, resolved on first use.</summary>
    private IDatabase _db => _remoteCache.Db;

    //Stream timestamps are written as round-trip UTC; parsing without these styles converts them to local time.
    private const System.Globalization.DateTimeStyles UtcTimestampStyles =
        System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal;
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
        // longer than they are useful. Anything older than MaxEventAgeMs is acknowledged
        // but dropped rather than delivered late.
        if (_commsAgentConfig.StaleEventDroppingEnabled)
        {
            var age = _timeProvider.GetUtcNow().UtcDateTime - commsEvent.TimestampUtc;
            if (age > TimeSpan.FromMilliseconds(_commsAgentConfig.MaxEventAgeMs))
            {
                _staleSinceNotice++;
                LogStreamEventStale(_logger, nameof(CommunicationsBgService), commsEvent.Source, (long)age.TotalSeconds, _staleSinceNotice);
                await MaybeSendDropNoticeAsync(cancellationToken);
                return;
            }
        }

        // Rate-limit producer-driven stream events to prevent message floods that the gateway
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

        // Events routed away from the chat group are operational diagnostics, not prompts, so they are
        // delivered directly; so is everything when no agent is configured.
        var group = _groupRouter.ResolveGroup(commsEvent);
        var routedAway = !string.Equals(group, _commsAgentConfig.GroupName, StringComparison.Ordinal);
        var agentAvailable = _agent is not null && _commsAgent is not null && _provider is not null;

        // Copy chat-bound events to the debug chat for observability; a routed event is already there.
        if (!routedAway)
            await _debugNotifier.SendStreamEventDebugAsync(commsEvent, cancellationToken);

        // Wait until the gateway serves the chat group before attempting delivery.
        await _groupReady.Task.WaitAsync(cancellationToken);

        var extraAttachments = await TryFetchMediaAttachmentAsync(commsEvent);

        if (routedAway || !agentAvailable)
        {
            if (routedAway)
                LogStreamEventRouted(_logger, nameof(CommunicationsBgService), commsEvent.Source);
            else
                LogNoAgentForwarding(_logger, nameof(CommunicationsBgService));
            _ = await _signalizrClient.SendAsync(group, _eventFormatter.Format(commsEvent), extraAttachments, cancellationToken);
            return;
        }

        var prompt = $"[{commsEvent.Source}] {commsEvent.Message}";
        if (commsEvent.JsonPayload is not null)
            prompt += $"\n\nJSON: {commsEvent.JsonPayload}";

        await EnqueueReplyAsync(prompt, extraBase64Attachments: extraAttachments, cancellationToken: cancellationToken);
    }

    /// <summary>Fetches the Redis-cached media an event references, deleting the key once read.</summary>
    /// <returns>The signal-cli data-URI attachment, or <see langword="null"/> when the event carries no media.</returns>
    private async Task<string[]?> TryFetchMediaAttachmentAsync(CommsEvent commsEvent)
    {
        if (commsEvent.JsonPayload is null)
            return null;
        try
        {
            // Deserialised directly rather than through FromJson, which logs every non-media payload as an error.
            var mediaRef = System.Text.Json.JsonSerializer.Deserialize<MediaReference>(commsEvent.JsonPayload);
            if (mediaRef?.MediaRedisKey is not { Length: > 0 } mediaKey)
                return null;

            var mediaBytes = (byte[]?)await _db.StringGetAsync(mediaKey);
            if (mediaBytes is not { Length: > 0 })
            {
                LogMediaNotFound(_logger, nameof(CommunicationsBgService), mediaKey);
                return null;
            }

            var mimeType = mediaRef.MimeType ?? "image/jpeg";
            var fileName = mediaRef.FileName ?? "media";
            await _db.KeyDeleteAsync(mediaKey, CommandFlags.FireAndForget);
            LogMediaAttached(_logger, nameof(CommunicationsBgService), mediaBytes.Length, mediaKey);
            return [$"data:{mimeType};filename={fileName};base64,{Convert.ToBase64String(mediaBytes)}"];
        }
        catch (System.Text.Json.JsonException)
        {
            // The payload is not a media reference, so the event is sent without an attachment.
            return null;
        }
        catch (RedisException ex)
        {
            LogMediaFetchFailed(_logger, ex, nameof(CommunicationsBgService), ex.GetType().Name);
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
            Environment = dict.GetValueOrDefault(nameof(CommsEvent.Environment)) ?? _env.GetAcronym(),
            TimestampUtc = DateTime.TryParse(dict.GetValueOrDefault(nameof(CommsEvent.TimestampUtc)), System.Globalization.CultureInfo.InvariantCulture, UtcTimestampStyles, out var ts)
                ? ts
                : _timeProvider.GetUtcNow().UtcDateTime,
            JsonPayload = dict.GetValueOrDefault(nameof(CommsEvent.JsonPayload)),
        };
    }

    /// <summary>
    /// Sends a single drop-notice message to the monitor group, or the chat group when none is
    /// configured, when stream events are being dropped (rate-limited and/or stale).
    /// </summary>
    /// <remarks>
    /// Rate-limited to at most once per <see cref="CommsAgentConfig.DropNoticeIntervalMs"/> so the
    /// notice itself cannot flood the group. The first drop always emits a notice immediately.
    /// </remarks>
    private async Task MaybeSendDropNoticeAsync(CancellationToken cancellationToken)
    {
        // The group must be ready before we can notify; until then drops are silent (counters
        // keep accumulating so the eventual notice reports the full total).
        if (!_groupReady.Task.IsCompletedSuccessfully)
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
            parts.Add($"{stale} older than {_commsAgentConfig.MaxEventAgeMs}ms");

        var notice = $"\uD83D\uDEA6 Dropped {total} notification(s) to avoid flooding the group \u2014 {string.Join(", ", parts)}.";

        try
        {
            // Drop notices are operator diagnostics, so they go to the monitor group when one is configured.
            var group = _commsAgentConfig.MonitorGroupName is { Length: > 0 } monitorGroupName
                ? monitorGroupName
                : _commsAgentConfig.GroupName;
            _ = await _signalizrClient.SendAsync(group, notice, cancellationToken);
            LogDropNoticeSent(_logger, nameof(CommunicationsBgService), total, true);
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
