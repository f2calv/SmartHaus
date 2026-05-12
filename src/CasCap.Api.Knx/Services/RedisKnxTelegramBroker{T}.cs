using StackExchange.Redis;

namespace CasCap.Services;

/// <summary>
/// Redis streams–backed <see cref="IKnxTelegramBroker{T}"/> implementation for cross-pod
/// Kubernetes deployments. Messages are serialized via the
/// <see cref="CasCap.Common.Extensions.JsonExtensions.ToJson{T}(T)"/> /
/// <see cref="CasCap.Common.Extensions.JsonExtensions.FromJson{T}(string)"/>
/// extensions and published to a date-partitioned Redis stream. Each day gets its own
/// stream key (<c>{baseKey}:{yyMMdd}</c>) with a configurable TTL so old streams are
/// automatically evicted. Consumers read via a consumer group ensuring at-least-once delivery.
/// </summary>
/// <typeparam name="T">The telegram type being transported.</typeparam>
public class RedisKnxTelegramBroker<T>(
    ILogger<RedisKnxTelegramBroker<T>> logger,
    IRemoteCache remoteCache,
    string baseStreamKey,
    string consumerGroup,
    string consumerGroupStartId,
    string readPosition,
    int readCount,
    int pollingDelayMs,
    int streamExpiryDays
    ) : IKnxTelegramBroker<T> where T : class
{
    private readonly IDatabase _db = remoteCache.Db;
    private readonly string _consumerName = $"{Environment.MachineName}-{AppDomain.CurrentDomain.FriendlyName}";

    // Track which date-partitioned stream keys have had their consumer group created
    private readonly HashSet<string> _groupCreatedKeys = [];

    /// <inheritdoc/>
    public async ValueTask PublishAsync(T item, CancellationToken cancellationToken = default)
    {
        var streamKey = $"{baseStreamKey}:{DateTime.UtcNow:yyMMdd}";
        var json = item.ToJson();
        await _db.StreamAddAsync(streamKey, [new NameValueEntry("data", json)]);
        await _db.KeyExpireAsync(streamKey, TimeSpan.FromDays(streamExpiryDays), flags: CommandFlags.FireAndForget);
        logger.LogTrace("{ClassName} published {Type} to {StreamKey}", nameof(RedisKnxTelegramBroker<T>), typeof(T).Name, streamKey);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<T> SubscribeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var currentDate = DateTime.UtcNow.Date;
        var streamKey = $"{baseStreamKey}:{currentDate:yyMMdd}";
        await EnsureConsumerGroupAsync(streamKey);

        while (!cancellationToken.IsCancellationRequested)
        {
            // Detect date rollover and switch to the new day's stream
            var nowDate = DateTime.UtcNow.Date;
            if (nowDate != currentDate)
            {
                logger.LogInformation("{ClassName} date rollover detected, switching from {OldKey} to {NewKey}",
                    nameof(RedisKnxTelegramBroker<T>),
                    $"{baseStreamKey}:{currentDate:yyMMdd}",
                    $"{baseStreamKey}:{nowDate:yyMMdd}");
                currentDate = nowDate;
                streamKey = $"{baseStreamKey}:{currentDate:yyMMdd}";
                await EnsureConsumerGroupAsync(streamKey);
            }

            StreamEntry[] entries;
            try
            {
                entries = await _db.StreamReadGroupAsync(streamKey, consumerGroup, _consumerName, readPosition, count: readCount);
            }
            catch (RedisServerException ex) when (ex.Message.Contains("NOGROUP"))
            {
                _groupCreatedKeys.Remove(streamKey);
                await EnsureConsumerGroupAsync(streamKey);
                continue;
            }

            if (entries.Length == 0)
            {
                await Task.Delay(pollingDelayMs, cancellationToken);
                continue;
            }

            foreach (var entry in entries)
            {
                var dataField = entry.Values.FirstOrDefault(v => v.Name == "data");
                if (dataField.Value.IsNullOrEmpty)
                    continue;

                T? item;
                try
                {
                    item = ((string)dataField.Value!).FromJson<T>();
                }
                catch (System.Text.Json.JsonException ex)
                {
                    logger.LogError(ex, "{ClassName} failed to deserialize entry {EntryId} from {StreamKey}",
                        nameof(RedisKnxTelegramBroker<T>), entry.Id, streamKey);
                    await _db.StreamAcknowledgeAsync(streamKey, consumerGroup, entry.Id);
                    continue;
                }

                if (item is not null)
                {
                    yield return item;
                    await _db.StreamAcknowledgeAsync(streamKey, consumerGroup, entry.Id);
                }
            }
        }
    }

    #region private helpers

    /// <summary>
    /// Ensures the consumer group exists on the given stream key, creating it if necessary.
    /// </summary>
    private async Task EnsureConsumerGroupAsync(string streamKey)
    {
        if (!_groupCreatedKeys.Add(streamKey)) return;
        try
        {
            await _db.StreamCreateConsumerGroupAsync(streamKey, consumerGroup, consumerGroupStartId, createStream: true);
            logger.LogInformation("{ClassName} created consumer group {ConsumerGroup} on {StreamKey}",
                nameof(RedisKnxTelegramBroker<T>), consumerGroup, streamKey);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            logger.LogDebug("{ClassName} consumer group {ConsumerGroup} already exists on {StreamKey}",
                nameof(RedisKnxTelegramBroker<T>), consumerGroup, streamKey);
        }
    }

    #endregion
}
