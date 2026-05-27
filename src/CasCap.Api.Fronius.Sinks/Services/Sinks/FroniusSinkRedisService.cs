using System.Runtime.CompilerServices;
using StackExchange.Redis;

namespace CasCap.Services;

/// <summary>
/// Event sink that persists <see cref="FroniusEvent"/> snapshot data and line items to Redis.
/// </summary>
[SinkType("Redis")]
public partial class FroniusSinkRedisService(
    ILogger<FroniusSinkRedisService> logger,
    IOptions<FroniusConfig> froniusConfig,
    TimeProvider timeProvider,
    IRemoteCache remoteCache
    ) : IEventSink<FroniusEvent>, IFroniusQuery
{
    /// <inheritdoc/>
    public string SinkType => "Redis";

    private readonly string? _summaryValues = froniusConfig.Value.Sinks.AvailableSinks.GetValueOrDefault("Redis")?.GetSetting(SinkSettingKeys.SnapshotValues);
    private readonly string? _seriesValues = froniusConfig.Value.Sinks.AvailableSinks.GetValueOrDefault("Redis")?.GetSetting(SinkSettingKeys.SeriesValues);

    /// <inheritdoc/>
    public async Task WriteEvent(FroniusEvent @event, CancellationToken cancellationToken = default)
    {
        LogWriteEvent(logger, nameof(FroniusSinkRedisService));
        if (string.IsNullOrWhiteSpace(_summaryValues))
        {
            LogSettingNotSet(logger, nameof(FroniusSinkRedisService), SinkSettingKeys.SnapshotValues);
            return;
        }

        await remoteCache.Db.HashSetAsync(_summaryValues,
            [
                new HashEntry(nameof(@event.SOC), @event.SOC.ToString()),
                new HashEntry(nameof(@event.P_Akku), @event.P_Akku.ToString()),
                new HashEntry(nameof(@event.P_Grid), @event.P_Grid.ToString()),
                new HashEntry(nameof(@event.P_Load), @event.P_Load.ToString()),
                new HashEntry(nameof(@event.P_PV), @event.P_PV.ToString()),
                new HashEntry(nameof(FroniusSnapshotEntity.ReadingUtc), @event.TimestampUtc.Ticks.ToString()),
            ],
            flags: CommandFlags.FireAndForget);

        // Store line item in sorted set per day
        var lineItemKey = $"{_seriesValues}:{@event.TimestampUtc:yyMMdd}";
        var lineItemValue = $"{@event.SOC}|{@event.P_Akku}|{@event.P_Grid}|{@event.P_Load}|{@event.P_PV}";
        await remoteCache.Db.SortedSetAddAsync(lineItemKey, lineItemValue, @event.TimestampUtc.Ticks, flags: CommandFlags.FireAndForget);
        await remoteCache.Db.KeyExpireAsync(lineItemKey, TimeSpan.FromDays(froniusConfig.Value.RedisSeriesExpiryDays), flags: CommandFlags.FireAndForget);
    }

    /// <summary>
    /// Retrieves the latest solar snapshot from Redis.
    /// </summary>
    public async Task<InverterSnapshot> GetSnapshot()
    {
        if (string.IsNullOrWhiteSpace(_summaryValues))
            return new();

        var entries = await remoteCache.Db.HashGetAllAsync(_summaryValues);
        var dict = entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());

        return new InverterSnapshot
        {
            StateOfCharge = TryGetDouble(dict, nameof(FroniusEvent.SOC)),
            BatteryPower = TryGetDouble(dict, nameof(FroniusEvent.P_Akku)),
            GridPower = TryGetDouble(dict, nameof(FroniusEvent.P_Grid)),
            LoadPower = TryGetDouble(dict, nameof(FroniusEvent.P_Load)),
            PhotovoltaicPower = TryGetDouble(dict, nameof(FroniusEvent.P_PV)),
            ReadingUtc = dict.TryGetValue(nameof(FroniusSnapshotEntity.ReadingUtc), out var ticks) && long.TryParse(ticks, out var t)
                ? new DateTimeOffset(t, TimeSpan.Zero)
                : null,
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<FroniusEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_seriesValues is null)
            yield break;
        var lineItemKey = $"{_seriesValues}:{timeProvider.GetUtcNow().UtcDateTime:yyMMdd}";
        var entries = await remoteCache.Db.SortedSetRangeByScoreWithScoresAsync(lineItemKey, order: Order.Descending, take: Math.Min(limit, 1000));

        foreach (var entry in entries)
        {
            var parts = entry.Element.ToString().Split('|');
            var timestampUtc = new DateTime((long)entry.Score, DateTimeKind.Utc);
            yield return new FroniusEvent(
                soc: double.Parse(parts[0]),
                pAkku: double.Parse(parts[1]),
                pGrid: double.Parse(parts[2]),
                pLoad: double.Parse(parts[3]),
                pPv: double.Parse(parts[4]),
                timestampUtc);
        }
    }

    #region private helpers

    private static double TryGetDouble(Dictionary<string, string> dict, string key)
        => dict.TryGetValue(key, out var raw) && double.TryParse(raw, out var result) ? result : 0;

    #endregion

    [LoggerMessage(Level = LogLevel.Trace, Message = "{ClassName} writing Fronius event to Redis")]
    private static partial void LogWriteEvent(ILogger logger, string className);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{ClassName} setting {SettingName} is not set")]
    private static partial void LogSettingNotSet(ILogger logger, string className, string settingName);
}
