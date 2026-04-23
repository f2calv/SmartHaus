using StackExchange.Redis;

namespace CasCap.Services;

/// <summary>
/// Event sink that persists <see cref="UbiquitiEvent"/> snapshot data and series
/// line items to Redis. Maintains running counts and last-event timestamps in a
/// Redis hash, and stores date-partitioned sorted sets per event type for time-series
/// retrieval.
/// </summary>
[SinkType("Redis")]
public class UbiquitiSinkRedisService(
    ILogger<UbiquitiSinkRedisService> logger,
    IOptions<UbiquitiConfig> ubiquitiConfig,
    IRemoteCache remoteCache
    ) : IEventSink<UbiquitiEvent>, IUbiquitiQuery
{
    private readonly string? _summaryValues = ubiquitiConfig.Value.Sinks.AvailableSinks.GetValueOrDefault("Redis")?.GetSetting(SinkSettingKeys.SnapshotValues);
    private readonly string? _seriesValues = ubiquitiConfig.Value.Sinks.AvailableSinks.GetValueOrDefault("Redis")?.GetSetting(SinkSettingKeys.SeriesValues);

    /// <inheritdoc/>
    public async Task WriteEvent(UbiquitiEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@UbiquitiEvent}", nameof(UbiquitiSinkRedisService), @event);
        if (string.IsNullOrWhiteSpace(_summaryValues))
        {
            logger.LogWarning("{ClassName} setting {SettingName} is not set", nameof(UbiquitiSinkRedisService), SinkSettingKeys.SnapshotValues);
            return;
        }

        var ticks = @event.DateCreatedUtc.Ticks.ToString();
        var (timestampField, countField) = @event.UbiquitiEventType switch
        {
            UbiquitiEventType.Motion => (nameof(UbiquitiSnapshotEntity.LastMotionUtc), nameof(UbiquitiSnapshotEntity.MotionCount)),
            UbiquitiEventType.SmartDetectPerson => (nameof(UbiquitiSnapshotEntity.LastSmartDetectPersonUtc), nameof(UbiquitiSnapshotEntity.SmartDetectPersonCount)),
            UbiquitiEventType.SmartDetectVehicle => (nameof(UbiquitiSnapshotEntity.LastSmartDetectVehicleUtc), nameof(UbiquitiSnapshotEntity.SmartDetectVehicleCount)),
            UbiquitiEventType.SmartDetectAnimal => (nameof(UbiquitiSnapshotEntity.LastSmartDetectAnimalUtc), nameof(UbiquitiSnapshotEntity.SmartDetectAnimalCount)),
            UbiquitiEventType.SmartDetectPackage => (nameof(UbiquitiSnapshotEntity.LastSmartDetectPackageUtc), nameof(UbiquitiSnapshotEntity.SmartDetectPackageCount)),
            UbiquitiEventType.Ring => (nameof(UbiquitiSnapshotEntity.LastRingUtc), nameof(UbiquitiSnapshotEntity.RingCount)),
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
            var eventTypeName = @event.UbiquitiEventType.ToString();
            var lineItemKey = $"{_seriesValues}:{@event.DateCreatedUtc:yyMMdd}:{eventTypeName}";
            await remoteCache.Db.SortedSetAddAsync(lineItemKey, eventTypeName, @event.DateCreatedUtc.Ticks, flags: CommandFlags.FireAndForget);
            await remoteCache.Db.KeyExpireAsync(lineItemKey, TimeSpan.FromDays(ubiquitiConfig.Value.RedisSeriesExpiryDays), flags: CommandFlags.FireAndForget);
        }
        else
            logger.LogWarning("{ClassName} setting {SettingName} is not set", nameof(UbiquitiSinkRedisService), SinkSettingKeys.SeriesValues);
    }

    /// <summary>
    /// Retrieves the latest Ubiquiti camera activity snapshot from Redis.
    /// </summary>
    public async Task<UbiquitiSnapshot> GetSnapshot()
    {
        if (string.IsNullOrWhiteSpace(_summaryValues))
            return new() { SnapshotUtc = DateTime.UtcNow };

        var entries = await remoteCache.Db.HashGetAllAsync(_summaryValues);
        var dict = entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());

        return new UbiquitiSnapshot
        {
            SnapshotUtc = DateTime.UtcNow,
            LastMotionUtc = TryGetDateTimeFromTicks(dict, nameof(UbiquitiSnapshotEntity.LastMotionUtc)),
            LastSmartDetectPersonUtc = TryGetDateTimeFromTicks(dict, nameof(UbiquitiSnapshotEntity.LastSmartDetectPersonUtc)),
            LastSmartDetectVehicleUtc = TryGetDateTimeFromTicks(dict, nameof(UbiquitiSnapshotEntity.LastSmartDetectVehicleUtc)),
            LastSmartDetectAnimalUtc = TryGetDateTimeFromTicks(dict, nameof(UbiquitiSnapshotEntity.LastSmartDetectAnimalUtc)),
            LastSmartDetectPackageUtc = TryGetDateTimeFromTicks(dict, nameof(UbiquitiSnapshotEntity.LastSmartDetectPackageUtc)),
            LastRingUtc = TryGetDateTimeFromTicks(dict, nameof(UbiquitiSnapshotEntity.LastRingUtc)),
            MotionCount = TryGetInt(dict, nameof(UbiquitiSnapshotEntity.MotionCount)),
            SmartDetectPersonCount = TryGetInt(dict, nameof(UbiquitiSnapshotEntity.SmartDetectPersonCount)),
            SmartDetectVehicleCount = TryGetInt(dict, nameof(UbiquitiSnapshotEntity.SmartDetectVehicleCount)),
            SmartDetectAnimalCount = TryGetInt(dict, nameof(UbiquitiSnapshotEntity.SmartDetectAnimalCount)),
            SmartDetectPackageCount = TryGetInt(dict, nameof(UbiquitiSnapshotEntity.SmartDetectPackageCount)),
            RingCount = TryGetInt(dict, nameof(UbiquitiSnapshotEntity.RingCount)),
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<UbiquitiEvent> GetEvents(string? id = null, int limit = 1000, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_seriesValues))
            yield break;

        if (id is null)
        {
            foreach (var eventType in Enum.GetValues<UbiquitiEventType>())
            {
                var lineItemKey = $"{_seriesValues}:{DateTime.UtcNow:yyMMdd}:{eventType}";
                var entries = await remoteCache.Db.SortedSetRangeByScoreWithScoresAsync(lineItemKey, order: Order.Descending, take: Math.Min(limit, 1000));
                foreach (var entry in entries)
                    yield return new UbiquitiEvent
                    {
                        UbiquitiEventType = eventType,
                        DateCreatedUtc = new DateTime((long)entry.Score, DateTimeKind.Utc),
                    };
            }
        }
        else if (Enum.TryParse<UbiquitiEventType>(id, ignoreCase: true, out var parsedType))
        {
            var lineItemKey = $"{_seriesValues}:{DateTime.UtcNow:yyMMdd}:{parsedType}";
            var entries = await remoteCache.Db.SortedSetRangeByScoreWithScoresAsync(lineItemKey, order: Order.Descending, take: Math.Min(limit, 1000));
            foreach (var entry in entries)
                yield return new UbiquitiEvent
                {
                    UbiquitiEventType = parsedType,
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
