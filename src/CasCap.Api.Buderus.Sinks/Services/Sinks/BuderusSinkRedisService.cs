using System.Runtime.CompilerServices;
using StackExchange.Redis;

namespace CasCap.Services;

/// <inheritdoc/>
[SinkType("Redis")]
public class BuderusSinkRedisService(
    ILogger<BuderusSinkRedisService> logger,
    IOptions<BuderusConfig> buderusConfig,
    IRemoteCache remoteCache
    ) : IEventSink<BuderusEvent>, IBuderusQuery
{
    private readonly string? _summaryValues = buderusConfig.Value.Sinks.AvailableSinks.GetValueOrDefault("Redis")?.GetSetting(SinkSettingKeys.SnapshotValues);
    private readonly string? _seriesValues = buderusConfig.Value.Sinks.AvailableSinks.GetValueOrDefault("Redis")?.GetSetting(SinkSettingKeys.SeriesValues);
    private readonly Dictionary<string, DatapointMapping> _datapointMappings = buderusConfig.Value.DatapointMappings;

    /// <inheritdoc/>
    public async Task WriteEvent(BuderusEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@BuderusEvent}", nameof(BuderusSinkRedisService), @event);
        if (string.IsNullOrWhiteSpace(_summaryValues))
        {
            logger.LogWarning("{ClassName} setting {SettingName} is not set", nameof(BuderusSinkRedisService), SinkSettingKeys.SnapshotValues);
            return;
        }

        await remoteCache.Db.HashSetAsync(_summaryValues, @event.Id, @event.Value.ToString(), flags: CommandFlags.FireAndForget);

        // Store line item in sorted set per day per datapoint
        var lineItemKey = $"{_seriesValues}:{@event.TimestampUtc:yyMMdd}:{@event.Id}";
        await remoteCache.Db.SortedSetAddAsync(lineItemKey, @event.Value.ToString(), @event.TimestampUtc.Ticks, flags: CommandFlags.FireAndForget);
        await remoteCache.Db.KeyExpireAsync(lineItemKey, TimeSpan.FromDays(buderusConfig.Value.RedisSeriesExpiryDays), flags: CommandFlags.FireAndForget);
    }

    /// <summary>
    /// Retrieves a typed snapshot of key Buderus heat pump sensor values from Redis.
    /// </summary>
    public async Task<BuderusSnapshot> GetSnapshot()
    {
        if (string.IsNullOrWhiteSpace(_summaryValues))
            return new();

        var entries = await remoteCache.Db.HashGetAllAsync(_summaryValues);
        var valuesByColumn = new Dictionary<string, string>(entries.Length);
        foreach (var entry in entries)
        {
            var datapointId = entry.Name.ToString();
            if (_datapointMappings.TryGetValue(datapointId, out var mapping))
                valuesByColumn[mapping.ColumnName] = entry.Value.ToString();
        }
        return BuderusSnapshot.FromValues(valuesByColumn);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<BuderusEvent> GetEvents(string? id = null, int limit = 1000, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_summaryValues))
            yield break;

        if (id is null)
        {
            // Snapshot: yield every hash entry as an event
            var entries = await remoteCache.Db.HashGetAllAsync(_summaryValues);
            foreach (var entry in entries)
                yield return new BuderusEvent(entry.Name.ToString(), entry.Value.ToString(), DateTime.UtcNow);
        }
        else
        {
            // Line items: controller passes underscore-separated partition key format; normalize to slash format for Redis key lookup
            var datapointId = id.Replace('_', '/');
            var lineItemKey = $"{_seriesValues}:{DateTime.UtcNow:yyMMdd}:{datapointId}";
            var entries = await remoteCache.Db.SortedSetRangeByScoreWithScoresAsync(lineItemKey, order: Order.Descending, take: Math.Min(limit, 1000));
            foreach (var entry in entries)
                yield return new BuderusEvent(id, entry.Element!, new DateTime((long)entry.Score, DateTimeKind.Utc));
        }
    }
}
