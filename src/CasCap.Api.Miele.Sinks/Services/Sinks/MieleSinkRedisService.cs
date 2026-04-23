using StackExchange.Redis;

namespace CasCap.Services;

/// <summary>Persists <see cref="MieleEvent"/> data to Redis (per-appliance snapshot hash + daily sorted set).</summary>
[SinkType("Redis")]
public class MieleSinkRedisService(ILogger<MieleSinkRedisService> logger,
    IOptions<MieleConfig> mieleConfig,
    IRemoteCache remoteCache) : IEventSink<MieleEvent>, IMieleQuery
{
    private readonly string? _snapshotValues = mieleConfig.Value.Sinks.AvailableSinks
        .GetValueOrDefault("Redis")?.GetSetting(SinkSettingKeys.SnapshotValues);
    private readonly string? _seriesValues = mieleConfig.Value.Sinks.AvailableSinks
        .GetValueOrDefault("Redis")?.GetSetting(SinkSettingKeys.SeriesValues);

    /// <inheritdoc/>
    public async Task WriteEvent(MieleEvent @event, CancellationToken cancellationToken = default)
    {
        logger.LogTrace("{ClassName} {@MieleEvent}", nameof(MieleSinkRedisService), @event);
        var db = remoteCache.Db;

        if (_snapshotValues is not null)
        {
            var key = $"{_snapshotValues}:{@event.DeviceId}";
            var entries = new List<HashEntry>
            {
                new(nameof(@event.DeviceId), @event.DeviceId),
                new(nameof(@event.EventType), ((int)@event.EventType).ToString()),
                new("ReadingUtc", @event.TimestampUtc.ToString("O")),
            };
            if (@event.DeviceName is not null) entries.Add(new(nameof(@event.DeviceName), @event.DeviceName));
            if (@event.StatusCode is not null) entries.Add(new(nameof(@event.StatusCode), @event.StatusCode.Value));
            if (@event.ProgramId is not null) entries.Add(new(nameof(@event.ProgramId), @event.ProgramId.Value));
            if (@event.ErrorCode is not null) entries.Add(new(nameof(@event.ErrorCode), @event.ErrorCode.Value));
            await db.HashSetAsync(key, entries.ToArray(), CommandFlags.FireAndForget);
        }

        if (_seriesValues is not null)
        {
            var dayKey = $"{_seriesValues}:{@event.TimestampUtc:yyMMdd}";
            var value = $"{@event.DeviceId}|{(int)@event.EventType}|{@event.StatusCode}|{@event.ProgramId}|{@event.ErrorCode}";
            await db.SortedSetAddAsync(dayKey, value, @event.TimestampUtc.Ticks, CommandFlags.FireAndForget);
            await db.KeyExpireAsync(dayKey, TimeSpan.FromDays(mieleConfig.Value.RedisSeriesExpiryDays), CommandFlags.FireAndForget);
        }
    }

    /// <inheritdoc/>
    public async Task<List<MieleSnapshot>> GetSnapshots()
    {
        if (_snapshotValues is null)
            return [];

        var snapshots = new List<MieleSnapshot>();
        var server = remoteCache.Db.Multiplexer.GetServer(remoteCache.Db.Multiplexer.GetEndPoints()[0]);
        await foreach (var key in server.KeysAsync(pattern: $"{_snapshotValues}:*"))
        {
            var hash = await remoteCache.Db.HashGetAllAsync(key);
            if (hash.Length == 0) continue;
            var dict = hash.ToDictionary(h => h.Name.ToString(), h => h.Value);
            snapshots.Add(new MieleSnapshot
            {
                DeviceId = dict.GetValueOrDefault("DeviceId"),
                DeviceName = dict.GetValueOrDefault("DeviceName"),
                StatusCode = TryGetInt(dict, "StatusCode"),
                ProgramId = TryGetInt(dict, "ProgramId"),
                ErrorCode = TryGetInt(dict, "ErrorCode"),
                ReadingUtc = dict.TryGetValue("ReadingUtc", out var r) && DateTimeOffset.TryParse(r, out var rv) ? rv : null,
            });
        }
        return snapshots;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<MieleEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_seriesValues is null) yield break;

        var dayKey = $"{_seriesValues}:{DateTime.UtcNow:yyMMdd}";
        var entries = await remoteCache.Db.SortedSetRangeByRankAsync(dayKey, 0, limit - 1, Order.Descending);
        foreach (var entry in entries)
        {
            var parts = entry.ToString().Split('|');
            if (parts.Length < 5) continue;
            yield return new MieleEvent
            {
                DeviceId = parts[0],
                EventType = int.TryParse(parts[1], out var et) ? (MieleEventType)et : MieleEventType.StatusUpdate,
                StatusCode = int.TryParse(parts[2], out var sc) ? sc : null,
                ProgramId = int.TryParse(parts[3], out var pid) ? pid : null,
                ErrorCode = int.TryParse(parts[4], out var ec) ? ec : null,
                TimestampUtc = DateTime.UtcNow,
            };
        }
    }

    #region private helpers

    private static int? TryGetInt(Dictionary<string, RedisValue> dict, string key) =>
        dict.TryGetValue(key, out var v) && int.TryParse((string?)v, out var i) ? i : null;

    #endregion
}
