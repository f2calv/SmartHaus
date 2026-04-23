using StackExchange.Redis;

namespace CasCap.Services;

/// <summary>Persists <see cref="WizEvent"/> data to Redis (per-bulb snapshot hash + daily sorted set).</summary>
[SinkType("Redis")]
public class WizSinkRedisService(ILogger<WizSinkRedisService> logger,
    IOptions<WizConfig> wizConfig,
    IRemoteCache remoteCache) : IEventSink<WizEvent>, IWizQuery
{
    private readonly string? _snapshotValues = wizConfig.Value.Sinks.AvailableSinks
        .GetValueOrDefault("Redis")?.GetSetting(SinkSettingKeys.SnapshotValues);
    private readonly string? _seriesValues = wizConfig.Value.Sinks.AvailableSinks
        .GetValueOrDefault("Redis")?.GetSetting(SinkSettingKeys.SeriesValues);

    /// <inheritdoc/>
    public async Task WriteEvent(WizEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@WizEvent}", nameof(WizSinkRedisService), @event);
        var db = remoteCache.Db;

        // Snapshot: per-bulb hash
        if (_snapshotValues is not null)
        {
            var key = $"{_snapshotValues}:{@event.DeviceId}";
            var entries = new List<HashEntry>
            {
                new(nameof(@event.DeviceId), @event.DeviceId),
                new(nameof(@event.IpAddress), @event.IpAddress),
                new(nameof(@event.State), @event.State.ToString()),
                new("ReadingUtc", @event.TimestampUtc.ToString("O")),
            };
            if (@event.Mac is not null) entries.Add(new(nameof(@event.Mac), @event.Mac));
            if (@event.Dimming is not null) entries.Add(new(nameof(@event.Dimming), @event.Dimming.Value));
            if (@event.SceneId is not null) entries.Add(new(nameof(@event.SceneId), @event.SceneId.Value));
            if (@event.Temp is not null) entries.Add(new(nameof(@event.Temp), @event.Temp.Value));
            if (@event.Rssi is not null) entries.Add(new(nameof(@event.Rssi), @event.Rssi.Value));
            await db.HashSetAsync(key, entries.ToArray(), CommandFlags.FireAndForget);
        }

        // Series: sorted set per day
        if (_seriesValues is not null)
        {
            var dayKey = $"{_seriesValues}:{@event.TimestampUtc:yyMMdd}";
            var value = $"{@event.DeviceId}|{@event.IpAddress}|{@event.State}|{@event.Dimming}|{@event.SceneId}|{@event.Temp}|{@event.Rssi}";
            await db.SortedSetAddAsync(dayKey, value, @event.TimestampUtc.Ticks, CommandFlags.FireAndForget);
            await db.KeyExpireAsync(dayKey, TimeSpan.FromDays(wizConfig.Value.RedisSeriesExpiryDays), CommandFlags.FireAndForget);
        }
    }

    /// <inheritdoc/>
    public async Task<List<WizSnapshot>> GetSnapshots()
    {
        if (_snapshotValues is null)
            return [];

        var snapshots = new List<WizSnapshot>();
        var server = remoteCache.Db.Multiplexer.GetServer(remoteCache.Db.Multiplexer.GetEndPoints()[0]);
        await foreach (var key in server.KeysAsync(pattern: $"{_snapshotValues}:*"))
        {
            var hash = await remoteCache.Db.HashGetAllAsync(key);
            if (hash.Length == 0) continue;
            var dict = hash.ToDictionary(h => h.Name.ToString(), h => h.Value);
            snapshots.Add(new WizSnapshot
            {
                DeviceId = dict.GetValueOrDefault("DeviceId"),
                IpAddress = dict.GetValueOrDefault("IpAddress"),
                Mac = dict.GetValueOrDefault("Mac"),
                State = dict.TryGetValue("State", out var s) && bool.TryParse(s, out var sv) && sv,
                Dimming = TryGetInt(dict, "Dimming"),
                SceneId = TryGetInt(dict, "SceneId"),
                Temp = TryGetInt(dict, "Temp"),
                Rssi = TryGetInt(dict, "Rssi"),
                ReadingUtc = dict.TryGetValue("ReadingUtc", out var r) && DateTimeOffset.TryParse(r, out var rv) ? rv : null,
            });
        }
        return snapshots;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WizEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_seriesValues is null) yield break;

        var dayKey = $"{_seriesValues}:{DateTime.UtcNow:yyMMdd}";
        var entries = await remoteCache.Db.SortedSetRangeByRankAsync(dayKey, 0, limit - 1, Order.Descending);
        foreach (var entry in entries)
        {
            var parts = entry.ToString().Split('|');
            if (parts.Length < 7) continue;
            yield return new WizEvent
            {
                DeviceId = parts[0],
                IpAddress = parts[1],
                State = bool.TryParse(parts[2], out var sv) && sv,
                Dimming = int.TryParse(parts[3], out var d) ? d : null,
                SceneId = int.TryParse(parts[4], out var sc) ? sc : null,
                Temp = int.TryParse(parts[5], out var t) ? t : null,
                Rssi = int.TryParse(parts[6], out var r) ? r : null,
                TimestampUtc = DateTime.UtcNow,
            };
        }
    }

    #region private helpers

    private static int? TryGetInt(Dictionary<string, RedisValue> dict, string key) =>
        dict.TryGetValue(key, out var v) && int.TryParse((string?)v, out var i) ? i : null;

    #endregion
}
