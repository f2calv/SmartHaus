using StackExchange.Redis;

namespace CasCap.Services;

public partial class CommunicationsBgService
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
            _logger.LogInformation("{ClassName} created consumer group {ConsumerGroup} on stream {StreamKey}",
                nameof(CommunicationsBgService), _commsAgentConfig.ConsumerGroup, _commsAgentConfig.StreamKey);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            _logger.LogDebug("{ClassName} consumer group {ConsumerGroup} already exists",
                nameof(CommunicationsBgService), _commsAgentConfig.ConsumerGroup);
        }
    }

    private async Task DrainStreamAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{ClassName} consuming stream {StreamKey} as {ConsumerGroup}/{ConsumerName}",
            nameof(CommunicationsBgService), _commsAgentConfig.StreamKey,
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
                _logger.LogWarning("{ClassName} consumer group disappeared, recreating", nameof(CommunicationsBgService));
                await EnsureConsumerGroupAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException)
            {
                _logger.LogError(ex, "{ClassName} error during stream read cycle", nameof(CommunicationsBgService));
                await Task.Delay(TimeSpan.FromMilliseconds(_commsAgentConfig.PollingIntervalMs), cancellationToken);
            }
        }
    }

    private async Task ProcessCommsEventAsync(CommsEvent commsEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("{ClassName} processing stream event from {Source}: {Message}",
            nameof(CommunicationsBgService), commsEvent.Source, commsEvent.Message);

        // Forward the raw stream event to the debug chat for observability.
        await _debugNotifier.SendStreamEventDebugAsync(commsEvent, cancellationToken);

        // Wait until Signal group resolution completes before attempting delivery.
        await _groupResolved.Task.WaitAsync(cancellationToken);

        if (_agent is null || _commsAgent is null || _provider is null)
        {
            // No agent — forward event message directly to the notification group.
            _logger.LogInformation("{ClassName} no agent configured, forwarding event directly to group",
                nameof(CommunicationsBgService));
            var directMsg = new SignalMessageRequest
            {
                Message = commsEvent.Message,
                Number = _signalCliConfig.PhoneNumber,
                Recipients = [_groupId!]
            };
            var sendResult = await _notifier.SendAsync(directMsg, cancellationToken);
            if (sendResult is null)
                _logger.LogWarning("{ClassName} direct forward SendAsync returned null — message may not have been delivered",
                    nameof(CommunicationsBgService));
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
                        _logger.LogDebug("{ClassName} attached {Size} byte media from {MediaRedisKey}",
                            nameof(CommunicationsBgService), mediaBytes.Length, mediaRef.MediaRedisKey);
                    }
                    else
                        _logger.LogWarning("{ClassName} media not found at {MediaRedisKey}, sending without attachment",
                            nameof(CommunicationsBgService), mediaRef.MediaRedisKey);
                }
            }
            catch { /* JsonPayload is not a MediaCommsPayload — that's fine, skip attachment */ }
        }

        EnqueueReply(prompt, extraBase64Attachments: extraAttachments);
    }

    private static CommsEvent DeserializeStreamEntry(StreamEntry entry)
    {
        var dict = entry.Values.ToDictionary(v => v.Name.ToString(), v => v.Value.ToString());
        return new CommsEvent
        {
            Source = dict.GetValueOrDefault(nameof(CommsEvent.Source)) ?? "Unknown",
            Message = dict.GetValueOrDefault(nameof(CommsEvent.Message)) ?? string.Empty,
            TimestampUtc = DateTime.TryParse(dict.GetValueOrDefault(nameof(CommsEvent.TimestampUtc)), out var ts)
                ? ts
                : DateTime.UtcNow,
            JsonPayload = dict.GetValueOrDefault(nameof(CommsEvent.JsonPayload)),
        };
    }
}
