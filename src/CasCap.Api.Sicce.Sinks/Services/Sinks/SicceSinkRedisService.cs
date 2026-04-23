using System.Runtime.CompilerServices;
using StackExchange.Redis;

namespace CasCap.Services;

/// <summary>
/// Event sink that persists <see cref="SicceEvent"/> snapshot data and line items to Redis.
/// </summary>
[SinkType("Redis")]
public class SicceSinkRedisService(
    ILogger<SicceSinkRedisService> logger,
    IOptions<SicceConfig> sicceConfig,
    IRemoteCache remoteCache
    ) : IEventSink<SicceEvent>, ISicceQuery
{
    private readonly string? _summaryValues = sicceConfig.Value.Sinks.AvailableSinks.GetValueOrDefault("Redis")?.GetSetting(SinkSettingKeys.SnapshotValues);
    private readonly string? _seriesValues = sicceConfig.Value.Sinks.AvailableSinks.GetValueOrDefault("Redis")?.GetSetting(SinkSettingKeys.SeriesValues);

    /// <inheritdoc/>
    public async Task WriteEvent(SicceEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@Data}", nameof(SicceSinkRedisService), @event);
        if (string.IsNullOrWhiteSpace(_summaryValues))
        {
            logger.LogWarning("{ClassName} setting {SettingName} is not set", nameof(SicceSinkRedisService), SinkSettingKeys.SnapshotValues);
            return;
        }

        await remoteCache.Db.HashSetAsync(_summaryValues,
            [
                new HashEntry(nameof(@event.Temperature), @event.Temperature.ToString()),
                new HashEntry(nameof(@event.Power), @event.Power.ToString()),
                new HashEntry(nameof(@event.IsOnline), @event.IsOnline.ToString()),
                new HashEntry(nameof(@event.PowerSwitch), @event.PowerSwitch.ToString()),
                new HashEntry(nameof(SicceSnapshotEntity.ReadingUtc), @event.TimestampUtc.Ticks.ToString()),
            ],
            flags: CommandFlags.FireAndForget);

        // Store line item in sorted set per day
        var lineItemKey = $"{_seriesValues}:{@event.TimestampUtc:yyMMdd}";
        var lineItemValue = $"{@event.Temperature}|{@event.Power}|{@event.IsOnline}|{@event.PowerSwitch}";
        await remoteCache.Db.SortedSetAddAsync(lineItemKey, lineItemValue, @event.TimestampUtc.Ticks, flags: CommandFlags.FireAndForget);
        await remoteCache.Db.KeyExpireAsync(lineItemKey, TimeSpan.FromDays(sicceConfig.Value.RedisSeriesExpiryDays), flags: CommandFlags.FireAndForget);
    }

    /// <summary>
    /// Retrieves the latest Sicce device snapshot from Redis.
    /// </summary>
    public async Task<SicceSnapshot> GetSnapshot()
    {
        if (string.IsNullOrWhiteSpace(_summaryValues))
            return new();

        var entries = await remoteCache.Db.HashGetAllAsync(_summaryValues);
        var dict = entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());

        return new SicceSnapshot
        {
            Temperature = TryGetDouble(dict, nameof(SicceEvent.Temperature)),
            Power = TryGetDouble(dict, nameof(SicceEvent.Power)),
            IsOnline = TryGetBool(dict, nameof(SicceEvent.IsOnline)),
            PowerSwitch = TryGetBool(dict, nameof(SicceEvent.PowerSwitch)),
            ReadingUtc = dict.TryGetValue(nameof(SicceSnapshotEntity.ReadingUtc), out var ticks) && long.TryParse(ticks, out var t)
                ? new DateTimeOffset(t, TimeSpan.Zero)
                : null,
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<SicceEvent> GetEvents(string? id = null, int limit = 1000, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_summaryValues))
            yield break;

        var lineItemKey = $"{_seriesValues}:{DateTime.UtcNow:yyMMdd}";
        var entries = await remoteCache.Db.SortedSetRangeByScoreWithScoresAsync(lineItemKey, order: Order.Descending, take: Math.Min(limit, 1000));

        foreach (var entry in entries)
        {
            var parts = entry.Element.ToString().Split('|');
            var timestampUtc = new DateTime((long)entry.Score, DateTimeKind.Utc);
            yield return new SicceEvent(
                temperature: double.Parse(parts[0]),
                power: double.Parse(parts[1]),
                isOnline: bool.Parse(parts[2]),
                powerSwitch: bool.Parse(parts[3]),
                timestampUtc);
        }
    }

    #region private helpers

    private static double TryGetDouble(Dictionary<string, string> dict, string key)
        => dict.TryGetValue(key, out var raw) && double.TryParse(raw, out var result) ? result : 0;

    private static bool TryGetBool(Dictionary<string, string> dict, string key)
        => dict.TryGetValue(key, out var raw) && bool.TryParse(raw, out var result) && result;

    #endregion
}
