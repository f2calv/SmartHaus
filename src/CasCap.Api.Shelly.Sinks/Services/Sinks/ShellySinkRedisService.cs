using System.Runtime.CompilerServices;
using StackExchange.Redis;

namespace CasCap.Services;

/// <summary>
/// Event sink that persists <see cref="ShellyEvent"/> snapshot data and line items to Redis.
/// Snapshot hashes are keyed per device. Line items include the device ID.
/// </summary>
[SinkType("Redis")]
public class ShellySinkRedisService(
    ILogger<ShellySinkRedisService> logger,
    IOptions<ShellyConfig> shellyConfig,
    IRemoteCache remoteCache
    ) : IEventSink<ShellyEvent>, IShellyQuery
{
    private readonly string? _summaryValues = shellyConfig.Value.Sinks.AvailableSinks.GetValueOrDefault("Redis")?.GetSetting(SinkSettingKeys.SnapshotValues);
    private readonly string? _seriesValues = shellyConfig.Value.Sinks.AvailableSinks.GetValueOrDefault("Redis")?.GetSetting(SinkSettingKeys.SeriesValues);

    /// <inheritdoc/>
    public async Task WriteEvent(ShellyEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} [{DeviceId}] {@Data}", nameof(ShellySinkRedisService), @event.DeviceId, @event);
        if (string.IsNullOrWhiteSpace(_summaryValues))
        {
            logger.LogWarning("{ClassName} setting {SettingName} is not set", nameof(ShellySinkRedisService), SinkSettingKeys.SnapshotValues);
            return;
        }

        var snapshotKey = $"{_summaryValues}:{@event.DeviceId}";
        await remoteCache.Db.HashSetAsync(snapshotKey,
            [
                new HashEntry(nameof(@event.DeviceId), @event.DeviceId),
                new HashEntry(nameof(@event.DeviceName), @event.DeviceName),
                new HashEntry(nameof(@event.Power), @event.Power.ToString()),
                new HashEntry(nameof(@event.RelayState), @event.RelayState.ToString()),
                new HashEntry(nameof(@event.Temperature), @event.Temperature.ToString()),
                new HashEntry(nameof(@event.Overpower), @event.Overpower.ToString()),
                new HashEntry(nameof(ShellySnapshotEntity.ReadingUtc), @event.TimestampUtc.Ticks.ToString()),
            ],
            flags: CommandFlags.FireAndForget);

        // Store line item in sorted set per day
        var lineItemKey = $"{_seriesValues}:{@event.TimestampUtc:yyMMdd}";
        var lineItemValue = $"{@event.DeviceId}|{@event.Power}|{@event.RelayState}|{@event.Temperature}|{@event.Overpower}";
        await remoteCache.Db.SortedSetAddAsync(lineItemKey, lineItemValue, @event.TimestampUtc.Ticks, flags: CommandFlags.FireAndForget);
        await remoteCache.Db.KeyExpireAsync(lineItemKey, TimeSpan.FromDays(shellyConfig.Value.RedisSeriesExpiryDays), flags: CommandFlags.FireAndForget);
    }

    /// <summary>
    /// Retrieves snapshots for all known devices from Redis.
    /// </summary>
    public async Task<List<ShellySnapshot>> GetSnapshots()
    {
        if (string.IsNullOrWhiteSpace(_summaryValues))
            return [];

        var snapshots = new List<ShellySnapshot>();
        var pattern = $"{_summaryValues}:*";
        await foreach (var key in remoteCache.Server.KeysAsync(pattern: pattern))
        {
            var entries = await remoteCache.Db.HashGetAllAsync(key);
            if (entries.Length == 0)
                continue;

            var dict = entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());
            snapshots.Add(new ShellySnapshot
            {
                DeviceId = dict.GetValueOrDefault(nameof(ShellyEvent.DeviceId)) ?? string.Empty,
                DeviceName = dict.GetValueOrDefault(nameof(ShellyEvent.DeviceName)) ?? string.Empty,
                Power = TryGetDouble(dict, nameof(ShellyEvent.Power)),
                IsOn = TryGetDouble(dict, nameof(ShellyEvent.RelayState)) >= 1,
                Temperature = TryGetDouble(dict, nameof(ShellyEvent.Temperature)),
                Overpower = TryGetDouble(dict, nameof(ShellyEvent.Overpower)) >= 1,
                ReadingUtc = dict.TryGetValue(nameof(ShellySnapshotEntity.ReadingUtc), out var ticks) && long.TryParse(ticks, out var t)
                    ? new DateTimeOffset(t, TimeSpan.Zero)
                    : null,
            });
        }
        return snapshots;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ShellyEvent> GetEvents(string? id = null, int limit = 1000, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_summaryValues))
            yield break;

        var lineItemKey = $"{_seriesValues}:{DateTime.UtcNow:yyMMdd}";
        var entries = await remoteCache.Db.SortedSetRangeByScoreWithScoresAsync(lineItemKey, order: Order.Descending, take: Math.Min(limit, 1000));

        foreach (var entry in entries)
        {
            var parts = entry.Element.ToString().Split('|');
            var timestampUtc = new DateTime((long)entry.Score, DateTimeKind.Utc);
            yield return new ShellyEvent(
                deviceId: parts.Length > 0 ? parts[0] : string.Empty,
                deviceName: string.Empty,
                power: parts.Length > 1 ? double.Parse(parts[1]) : 0,
                relayState: parts.Length > 2 ? double.Parse(parts[2]) : 0,
                temperature: parts.Length > 3 ? double.Parse(parts[3]) : 0,
                overpower: parts.Length > 4 ? double.Parse(parts[4]) : 0,
                timestampUtc);
        }
    }

    #region private helpers

    private static double TryGetDouble(Dictionary<string, string> dict, string key)
        => dict.TryGetValue(key, out var raw) && double.TryParse(raw, out var result) ? result : 0;

    #endregion
}
