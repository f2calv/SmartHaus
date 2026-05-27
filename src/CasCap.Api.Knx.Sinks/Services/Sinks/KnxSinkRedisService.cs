using StackExchange.Redis;

namespace CasCap.Services;

/// <summary>
/// Event sink that persists <see cref="KnxEvent"/> state to Redis via <see cref="IKnxState"/>
/// and supports retrieval of the latest snapshot per group address.
/// </summary>
[SinkType("Redis")]
public partial class KnxSinkRedisService(
    ILogger<KnxSinkRedisService> logger,
    IOptions<KnxConfig> knxConfig,
    IRemoteCache remoteCache,
    IKnxState knxState
    ) : IEventSink<KnxEvent>
{
    /// <inheritdoc/>
    public string SinkType => "Redis";

    private readonly string? _seriesValues = knxConfig.Value.Sinks.AvailableSinks.GetValueOrDefault("Redis")?.GetSetting(SinkSettingKeys.SeriesValues);

    /// <inheritdoc/>
    public async Task WriteEvent(KnxEvent @event, CancellationToken cancellationToken = default)
    {
        LogWriteEventStart(logger, nameof(KnxSinkRedisService), @event.Kga.Name);
        await knxState.SetKnxState(@event.Kga.Name, @event.TimestampUtc, @event.ValueAsString, @event.ValueLabel);

        // Store line item in sorted set per day per group address
        if (!string.IsNullOrWhiteSpace(_seriesValues))
        {
            var lineItemKey = $"{_seriesValues}:{@event.TimestampUtc:yyMMdd}:{@event.Kga.Name}";
            await remoteCache.Db.SortedSetAddAsync(lineItemKey, @event.ValueAsString, @event.TimestampUtc.Ticks, flags: CommandFlags.FireAndForget);
            await remoteCache.Db.KeyExpireAsync(lineItemKey, TimeSpan.FromDays(knxConfig.Value.RedisSeriesExpiryDays), flags: CommandFlags.FireAndForget);
        }
        else
            LogSettingNotSet(logger, nameof(KnxSinkRedisService), SinkSettingKeys.SeriesValues);

        LogWriteEventEnd(logger, nameof(KnxSinkRedisService));
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KnxEvent> GetEvents(string? id = null, int limit = 1000, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (id is not null)
        {
            var state = await knxState.GetKnxState(id, cancellationToken);
            if (state is not null)
                yield return ToKnxEvent(state);
            yield break;
        }

        var allState = await knxState.GetAllState(cancellationToken);
        foreach (var state in allState.Values.Take(limit))
            yield return ToKnxEvent(state);
    }

    #region private helpers

    private static KnxEvent ToKnxEvent(State state)
    {
        var args = new KnxGroupEvent { SourceAddress = new KnxSourceAddress { IndividualAddress = string.Empty } };
        var kga = new KnxGroupAddressParsed { Name = state.GroupAddress };
        return new KnxEvent(state.TimestampUtc, args, kga, default!, state.Value, state.ValueLabel);
    }

    #endregion

    [LoggerMessage(Level = LogLevel.Trace, Message = "{ClassName} starting send to redis for {GroupAddressName}")]
    private static partial void LogWriteEventStart(ILogger logger, string className, string groupAddressName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{ClassName} setting {SettingName} is not set")]
    private static partial void LogSettingNotSet(ILogger logger, string className, string settingName);

    [LoggerMessage(Level = LogLevel.Trace, Message = "{ClassName} finished send to redis")]
    private static partial void LogWriteEventEnd(ILogger logger, string className);
}
