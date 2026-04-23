using System.Runtime.CompilerServices;
using StackExchange.Redis;

namespace CasCap.Services;

/// <summary>
/// Event sink that persists <see cref="DoorBirdEvent"/> snapshot data and series
/// line items to Redis. Maintains running counts and last-event timestamps in a
/// Redis hash, and stores date-partitioned sorted sets per event type for time-series
/// retrieval.
/// </summary>
[SinkType("Redis")]
public class DoorBirdSinkRedisService(
    ILogger<DoorBirdSinkRedisService> logger,
    IOptions<DoorBirdConfig> doorBirdConfig,
    IRemoteCache remoteCache
    ) : IEventSink<DoorBirdEvent>, IDoorBirdQuery
{
    private readonly string? _summaryValues = doorBirdConfig.Value.Sinks.AvailableSinks.GetValueOrDefault("Redis")?.GetSetting(SinkSettingKeys.SnapshotValues);
    private readonly string? _seriesValues = doorBirdConfig.Value.Sinks.AvailableSinks.GetValueOrDefault("Redis")?.GetSetting(SinkSettingKeys.SeriesValues);

    /// <inheritdoc/>
    public async Task WriteEvent(DoorBirdEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@DoorBirdEvent}", nameof(DoorBirdSinkRedisService), @event);
        if (string.IsNullOrWhiteSpace(_summaryValues))
        {
            logger.LogWarning("{ClassName} setting {SettingName} is not set", nameof(DoorBirdSinkRedisService), SinkSettingKeys.SnapshotValues);
            return;
        }

        var ticks = @event.DateCreatedUtc.Ticks.ToString();
        var (timestampField, countField) = @event.DoorBirdEventType switch
        {
            DoorBirdEventType.Doorbell => (nameof(DoorBirdSnapshotEntity.LastDoorbellUtc), nameof(DoorBirdSnapshotEntity.DoorbellCount)),
            DoorBirdEventType.MotionSensor => (nameof(DoorBirdSnapshotEntity.LastMotionUtc), nameof(DoorBirdSnapshotEntity.MotionCount)),
            DoorBirdEventType.Rfid => (nameof(DoorBirdSnapshotEntity.LastRfidUtc), nameof(DoorBirdSnapshotEntity.RfidCount)),
            DoorBirdEventType.DoorRelay => (nameof(DoorBirdSnapshotEntity.LastRelayTriggerUtc), nameof(DoorBirdSnapshotEntity.RelayTriggerCount)),
            _ => ((string?)null, (string?)null),
        };

        if (timestampField is null)
            return;

        // Snapshot: increment count atomically and set timestamp
        await remoteCache.Db.HashIncrementAsync(_summaryValues, countField);
        await remoteCache.Db.HashSetAsync(_summaryValues, timestampField, ticks, flags: CommandFlags.FireAndForget);

        // Series: store line item in sorted set per day per event type
        if (!string.IsNullOrWhiteSpace(_seriesValues))
        {
            var eventTypeName = @event.DoorBirdEventType.ToString();
            var lineItemKey = $"{_seriesValues}:{@event.DateCreatedUtc:yyMMdd}:{eventTypeName}";
            await remoteCache.Db.SortedSetAddAsync(lineItemKey, eventTypeName, @event.DateCreatedUtc.Ticks, flags: CommandFlags.FireAndForget);
            await remoteCache.Db.KeyExpireAsync(lineItemKey, TimeSpan.FromDays(doorBirdConfig.Value.RedisSeriesExpiryDays), flags: CommandFlags.FireAndForget);
        }
        else
            logger.LogWarning("{ClassName} setting {SettingName} is not set", nameof(DoorBirdSinkRedisService), SinkSettingKeys.SeriesValues);
    }

    /// <summary>
    /// Retrieves the latest DoorBird activity snapshot from Redis.
    /// </summary>
    public async Task<DoorBirdSnapshot> GetSnapshot()
    {
        if (string.IsNullOrWhiteSpace(_summaryValues))
            return new() { SnapshotUtc = DateTime.UtcNow };

        var entries = await remoteCache.Db.HashGetAllAsync(_summaryValues);
        var dict = entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());

        return new DoorBirdSnapshot
        {
            SnapshotUtc = DateTime.UtcNow,
            LastDoorbellUtc = TryGetDateTimeFromTicks(dict, nameof(DoorBirdSnapshotEntity.LastDoorbellUtc)),
            LastMotionUtc = TryGetDateTimeFromTicks(dict, nameof(DoorBirdSnapshotEntity.LastMotionUtc)),
            LastRfidUtc = TryGetDateTimeFromTicks(dict, nameof(DoorBirdSnapshotEntity.LastRfidUtc)),
            LastRelayTriggerUtc = TryGetDateTimeFromTicks(dict, nameof(DoorBirdSnapshotEntity.LastRelayTriggerUtc)),
            DoorbellCount = TryGetInt(dict, nameof(DoorBirdSnapshotEntity.DoorbellCount)),
            MotionCount = TryGetInt(dict, nameof(DoorBirdSnapshotEntity.MotionCount)),
            RfidCount = TryGetInt(dict, nameof(DoorBirdSnapshotEntity.RfidCount)),
            RelayTriggerCount = TryGetInt(dict, nameof(DoorBirdSnapshotEntity.RelayTriggerCount)),
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<DoorBirdEvent> GetEvents(string? id = null, int limit = 1000, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_seriesValues))
            yield break;

        if (id is null)
        {
            // Yield today's events across all event types
            foreach (var eventType in Enum.GetValues<DoorBirdEventType>())
            {
                var lineItemKey = $"{_seriesValues}:{DateTime.UtcNow:yyMMdd}:{eventType}";
                var entries = await remoteCache.Db.SortedSetRangeByScoreWithScoresAsync(lineItemKey, order: Order.Descending, take: Math.Min(limit, 1000));
                foreach (var entry in entries)
                    yield return new DoorBirdEvent
                    {
                        DoorBirdEventType = eventType,
                        DateCreatedUtc = new DateTime((long)entry.Score, DateTimeKind.Utc),
                    };
            }
        }
        else if (Enum.TryParse<DoorBirdEventType>(id, ignoreCase: true, out var parsedType))
        {
            var lineItemKey = $"{_seriesValues}:{DateTime.UtcNow:yyMMdd}:{parsedType}";
            var entries = await remoteCache.Db.SortedSetRangeByScoreWithScoresAsync(lineItemKey, order: Order.Descending, take: Math.Min(limit, 1000));
            foreach (var entry in entries)
                yield return new DoorBirdEvent
                {
                    DoorBirdEventType = parsedType,
                    DateCreatedUtc = new DateTime((long)entry.Score, DateTimeKind.Utc),
                };
        }
    }

    #region private helpers

    private static DateTime? TryGetDateTimeFromTicks(Dictionary<string, string> dict, string key)
        => dict.TryGetValue(key, out var raw) && long.TryParse(raw, out var ticks) ? new DateTime(ticks, DateTimeKind.Utc) : null;

    private static int TryGetInt(Dictionary<string, string> dict, string key)
        => dict.TryGetValue(key, out var raw) && int.TryParse(raw, out var result) ? result : 0;

    #endregion
}
