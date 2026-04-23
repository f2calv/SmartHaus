using StackExchange.Redis;

namespace CasCap.Services;

/// <summary>
/// Event sink that persists <see cref="EdgeHardwareEvent"/> snapshot data and line items to Redis.
/// </summary>
[SinkType("Redis")]
public class EdgeHardwareSinkRedisService(
    ILogger<EdgeHardwareSinkRedisService> logger,
    IOptions<EdgeHardwareConfig> edgeHardwareConfig,
    IRemoteCache remoteCache
    ) : IEventSink<EdgeHardwareEvent>, IEdgeHardwareQuery
{
    private readonly string? _snapshotValues = edgeHardwareConfig.Value.Sinks.AvailableSinks.GetValueOrDefault("Redis")?.GetSetting(SinkSettingKeys.SnapshotValues);
    private readonly string? _seriesValues = edgeHardwareConfig.Value.Sinks.AvailableSinks.GetValueOrDefault("Redis")?.GetSetting(SinkSettingKeys.SeriesValues);

    /// <inheritdoc/>
    public async Task WriteEvent(EdgeHardwareEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@Data}", nameof(EdgeHardwareSinkRedisService), @event);
        if (string.IsNullOrWhiteSpace(_snapshotValues))
        {
            logger.LogWarning("{ClassName} setting {SettingName} is not set", nameof(EdgeHardwareSinkRedisService), SinkSettingKeys.SnapshotValues);
            return;
        }

        var entries = new List<HashEntry>
        {
            new(nameof(EdgeHardwareSnapshotEntity.ReadingUtc), @event.TimestampUtc.Ticks.ToString()),
        };
        entries.Add(new(nameof(@event.NodeName), @event.NodeName));
        if (@event.GpuPowerDrawW.HasValue)
            entries.Add(new(nameof(@event.GpuPowerDrawW), @event.GpuPowerDrawW.Value.ToString()));
        if (@event.GpuTemperatureC.HasValue)
            entries.Add(new(nameof(@event.GpuTemperatureC), @event.GpuTemperatureC.Value.ToString()));
        if (@event.GpuUtilizationPercent.HasValue)
            entries.Add(new(nameof(@event.GpuUtilizationPercent), @event.GpuUtilizationPercent.Value.ToString()));
        if (@event.GpuMemoryUtilizationPercent.HasValue)
            entries.Add(new(nameof(@event.GpuMemoryUtilizationPercent), @event.GpuMemoryUtilizationPercent.Value.ToString()));
        if (@event.GpuMemoryUsedMiB.HasValue)
            entries.Add(new(nameof(@event.GpuMemoryUsedMiB), @event.GpuMemoryUsedMiB.Value.ToString()));
        if (@event.GpuMemoryTotalMiB.HasValue)
            entries.Add(new(nameof(@event.GpuMemoryTotalMiB), @event.GpuMemoryTotalMiB.Value.ToString()));
        if (@event.CpuTemperatureC.HasValue)
            entries.Add(new(nameof(@event.CpuTemperatureC), @event.CpuTemperatureC.Value.ToString()));

        var snapshotKey = $"{_snapshotValues}:{@event.NodeName}";
        await remoteCache.Db.HashSetAsync(snapshotKey, entries.ToArray(), flags: CommandFlags.FireAndForget);

        // Store line item in sorted set per day
        var lineItemKey = $"{_seriesValues}:{@event.TimestampUtc:yyMMdd}";
        var lineItemValue = BuildLineItemValue(@event);
        await remoteCache.Db.SortedSetAddAsync(lineItemKey, lineItemValue, @event.TimestampUtc.Ticks, flags: CommandFlags.FireAndForget);
    }

    /// <inheritdoc/>
    public async Task<List<EdgeHardwareSnapshot>> GetSnapshots()
    {
        if (string.IsNullOrWhiteSpace(_snapshotValues))
            return [];

        var snapshots = new List<EdgeHardwareSnapshot>();
        var pattern = $"{_snapshotValues}:*";
        await foreach (var key in remoteCache.Server.KeysAsync(pattern: pattern))
        {
            var entries = await remoteCache.Db.HashGetAllAsync(key);
            if (entries.Length == 0)
                continue;

            var dict = entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());
            snapshots.Add(new EdgeHardwareSnapshot
            {
                NodeName = dict.GetValueOrDefault(nameof(EdgeHardwareEvent.NodeName)),
                GpuPowerDrawW = TryGetDouble(dict, nameof(EdgeHardwareEvent.GpuPowerDrawW)),
                GpuTemperatureC = TryGetDouble(dict, nameof(EdgeHardwareEvent.GpuTemperatureC)),
                GpuUtilizationPercent = TryGetDouble(dict, nameof(EdgeHardwareEvent.GpuUtilizationPercent)),
                GpuMemoryUtilizationPercent = TryGetDouble(dict, nameof(EdgeHardwareEvent.GpuMemoryUtilizationPercent)),
                GpuMemoryUsedMiB = TryGetDouble(dict, nameof(EdgeHardwareEvent.GpuMemoryUsedMiB)),
                GpuMemoryTotalMiB = TryGetDouble(dict, nameof(EdgeHardwareEvent.GpuMemoryTotalMiB)),
                CpuTemperatureC = TryGetDouble(dict, nameof(EdgeHardwareEvent.CpuTemperatureC)),
                Timestamp = dict.TryGetValue(nameof(EdgeHardwareSnapshotEntity.ReadingUtc), out var ticks) && long.TryParse(ticks, out var t)
                    ? new DateTimeOffset(t, TimeSpan.Zero)
                    : DateTimeOffset.MinValue,
            });
        }
        return snapshots;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<EdgeHardwareEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_snapshotValues))
            yield break;

        var lineItemKey = $"{_seriesValues}:{DateTime.UtcNow:yyMMdd}";
        var entries = await remoteCache.Db.SortedSetRangeByScoreWithScoresAsync(lineItemKey, order: Order.Descending, take: Math.Min(limit, 1000));

        foreach (var entry in entries)
        {
            var timestampUtc = new DateTime((long)entry.Score, DateTimeKind.Utc);
            yield return ParseLineItemValue(entry.Element.ToString()!, timestampUtc);
        }
    }

    #region private helpers

    private static double? TryGetDouble(Dictionary<string, string> dict, string key)
        => dict.TryGetValue(key, out var raw) && double.TryParse(raw, out var result) ? result : null;

    private static string BuildLineItemValue(EdgeHardwareEvent data)
        => $"{data.NodeName}|{data.GpuPowerDrawW}|{data.GpuTemperatureC}|{data.GpuUtilizationPercent}|{data.GpuMemoryUtilizationPercent}|{data.GpuMemoryUsedMiB}|{data.GpuMemoryTotalMiB}|{data.CpuTemperatureC}";

    private static EdgeHardwareEvent ParseLineItemValue(string value, DateTime timestampUtc)
    {
        var parts = value.Split('|');
        return new EdgeHardwareEvent
        {
            NodeName = parts.Length > 0 && !string.IsNullOrEmpty(parts[0]) ? parts[0] : "unknown",
            TimestampUtc = timestampUtc,
            GpuPowerDrawW = parts.Length > 1 ? TryParseDouble(parts[1]) : null,
            GpuTemperatureC = parts.Length > 2 ? TryParseDouble(parts[2]) : null,
            GpuUtilizationPercent = parts.Length > 3 ? TryParseDouble(parts[3]) : null,
            GpuMemoryUtilizationPercent = parts.Length > 4 ? TryParseDouble(parts[4]) : null,
            GpuMemoryUsedMiB = parts.Length > 5 ? TryParseDouble(parts[5]) : null,
            GpuMemoryTotalMiB = parts.Length > 6 ? TryParseDouble(parts[6]) : null,
            CpuTemperatureC = parts.Length > 7 ? TryParseDouble(parts[7]) : null,
        };
    }

    private static double? TryParseDouble(string value)
        => double.TryParse(value, out var result) ? result : null;

    #endregion
}
