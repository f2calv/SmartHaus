using StackExchange.Redis;

namespace CasCap.Services;

/// <summary>
/// <see cref="KnxRedisStateService"/> is intended to be the single source of truth regarding the state of all the group addresses on the KNX Bus.
/// </summary>
public class KnxRedisStateService : IKnxState
{
    private readonly ILogger _logger;
    private readonly IRemoteCache _remoteCache;
    private readonly IEventSink<KnxEvent> _azTablesSink;
    private readonly SinkConfigParams _redisConfig;
    private readonly KnxGroupAddressLookupService _knxGroupAddressLookupSvc;

    /// <summary>Initialises a new instance of the <see cref="KnxRedisStateService"/> class.</summary>
    public KnxRedisStateService(ILogger<KnxRedisStateService> logger,
        IOptions<KnxConfig> config,
        [FromKeyedServices("AzureTables")] IEventSink<KnxEvent> azTablesSink,
        KnxGroupAddressLookupService knxGroupAddressLookupSvc,
        IRemoteCache remoteCache
    )
    {
        _logger = logger;
        _redisConfig = config.Value.Sinks.AvailableSinks["Redis"];
        _remoteCache = remoteCache;
        _azTablesSink = azTablesSink;
        _knxGroupAddressLookupSvc = knxGroupAddressLookupSvc;
        //IsStateSynced = _env.IsDevelopment();//bypass state sync when running locally?

        LoadCustomLuaScripts();
    }

    /// <summary>
    /// Loads the <see cref="GetKnxState(string, CancellationToken)"/>
    /// and <see cref="SetKnxState(string, DateTime, string, string)"/> Lua scripts
    /// into the <see cref="IRemoteCache"/> script cache.
    /// </summary>
    private void LoadCustomLuaScripts()
    {
        var scriptNames = new[] { $"CasCap.Resources.{nameof(GetKnxState)}.lua", $"CasCap.Resources.{nameof(SetKnxState)}.lua" };
        foreach (var scriptName in scriptNames)
        {
            var script = GetType().Assembly.GetManifestResourceString(scriptName);
            _remoteCache.LoadLuaScript(scriptName, script!);
        }
    }

    /// <inheritdoc/>
    public async Task SetKnxState(string groupAddressName, DateTime timestampUtc, string valueDecoded, string? valueLabelDecoded)
    {
        if (groupAddressName is null)
            throw new ArgumentException($"{groupAddressName} param should not be null", nameof(groupAddressName));
        if (valueDecoded is null)
            throw new ArgumentException($"{groupAddressName} param should not be null", nameof(valueDecoded));
        valueLabelDecoded ??= string.Empty;//this can be null, but we can't send null into a Lua script
        var luaScript = _remoteCache.LuaScripts[$"CasCap.Resources.{nameof(SetKnxState)}.lua"];

        _ = await luaScript.EvaluateAsync(_remoteCache.Db, new
        {
            hsKeyValue = (RedisKey)_redisConfig.GetSetting(SinkSettingKeys.SnapshotValues),
            hsKeyTimestampUtc = (RedisKey)_redisConfig.GetSetting(KnxSinkKeys.SnapshotTimestamps),
            hsKeyValueLabel = (RedisKey)_redisConfig.GetSetting(KnxSinkKeys.SnapshotStrings),

            keyGroupAddress = (RedisKey)groupAddressName,

            valueValue = (RedisKey)valueDecoded,
            valueTimestampUtc = (RedisKey)timestampUtc.Ticks.ToString(),
            valueValueLabel = (RedisKey)valueLabelDecoded,

            //Observability: per-day call counter aligned with CasCap.Common.Caching pattern
            trackKey = (RedisValue)$"stats:knx:{DateTime.UtcNow:yyMMdd}",
            trackCaller = (RedisValue)nameof(SetKnxState)
        }, flags: CommandFlags.FireAndForget);
    }

    /// <summary>
    /// Gets all hash entries for a given Redis key.
    /// </summary>
    private async Task<Dictionary<string, string>> GetHashAll(string key)
    {
        var hs = await _remoteCache.Db.HashGetAllAsync(key);
        return hs.ToDictionary(k => k.Name.ToString(), v => v.Value.ToString());
    }

    /// <summary>
    /// Sets a string key with an expiry, used for blocking/debouncing.
    /// </summary>
    private async Task SetBlock(string key, string value, TimeSpan expiry)
        => _ = await _remoteCache.Db.StringSetAsync(key, value, expiry, flags: CommandFlags.FireAndForget);

    /// <summary>
    /// Checks if a string key exists, used for blocking/debouncing.
    /// </summary>
    private async Task<bool> IsBlocked(string key)
    {
        var result = await _remoteCache.Db.StringGetAsync(key);
        return result.HasValue;
    }

    /// <inheritdoc/>
    public async Task<State?> GetKnxState(string groupAddressName, CancellationToken cancellationToken = default)
    {
        await EnsureStateSynced(cancellationToken);

        var luaScript = _remoteCache.LuaScripts[$"CasCap.Resources.{nameof(GetKnxState)}.lua"];

        var result = (RedisResult[]?)await luaScript.EvaluateAsync(_remoteCache.Db, new
        {
            hsKeyValue = (RedisKey)_redisConfig.GetSetting(SinkSettingKeys.SnapshotValues),
            hsKeyTimestampUtc = (RedisKey)_redisConfig.GetSetting(KnxSinkKeys.SnapshotTimestamps),
            hsKeyValueLabel = (RedisKey)_redisConfig.GetSetting(KnxSinkKeys.SnapshotStrings),

            keyGroupAddress = (RedisKey)groupAddressName,

            //Observability: per-day call counter aligned with CasCap.Common.Caching pattern
            trackKey = (RedisValue)$"stats:knx:{DateTime.UtcNow:yyMMdd}",
            trackCaller = (RedisValue)nameof(GetKnxState)
        });

        if (result is null || result.Length < 2 || result[0].IsNull || result[1].IsNull)
        {
            _logger.LogDebug("{ClassName} no state found for '{GroupAddressName}'", nameof(KnxRedisStateService), groupAddressName);
            return null;
        }

        var value = (string?)result[0];
        var timestamp = (string?)result[1];
        var valueLabel = result.Length > 2 ? (string?)result[2] : null;
        var utcDatetime = new DateTime(long.Parse(timestamp!), DateTimeKind.Utc);

        return new State(groupAddressName, value!, valueLabel, utcDatetime);
    }

    private bool IsStateSynced = false;
    private static readonly SemaphoreSlim semaphoreSlim = new(1, 1);

    /// <summary>
    /// Ensures that state has been synced from Azure Table Storage to Redis at least once.
    /// Subsequent calls are a no-op.
    /// </summary>
    private async Task EnsureStateSynced(CancellationToken cancellationToken)
    {
        if (!IsStateSynced)
        {
            await semaphoreSlim.WaitAsync(cancellationToken);
            if (IsStateSynced) return;
            _logger.LogWarning("{ClassName} performing one-time sync from Azure Table Storage to Redis", nameof(KnxRedisStateService));
            try
            {
                await SyncState(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ClassName} failed to sync state from Azure Table Storage to Redis", nameof(KnxRedisStateService));
                return;
            }
            IsStateSynced = true;
            semaphoreSlim.Release();
        }
    }

    /// <summary>
    /// Builds a dictionary of all <see cref="State"/> objects from the snapshot values, strings and timestamps hashes.
    /// </summary>
    /// <inheritdoc/>
    public async Task<Dictionary<string, State>> GetAllState(CancellationToken cancellationToken = default)
    {
        await EnsureStateSynced(cancellationToken);
        return await GetAllRedisState();
    }

    /// <summary>
    /// Synchronises state from Azure Table Storage to Redis.
    /// </summary>
    private async Task SyncState(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        // Populate the group address lookup before iterating events — GetKGAByName only
        // checks the in-memory dictionary and will miss everything if GetLookup hasn't run yet.
        var lookup = await _knxGroupAddressLookupSvc.GetLookup(cancellationToken);
        var validNames = new HashSet<string>(lookup.Values.Select(kga => kga.Name));

        var sourceStates = new List<State>();
        await foreach (var knxEvent in _azTablesSink.GetEvents(cancellationToken: cancellationToken))
        {
            if (_knxGroupAddressLookupSvc.GetKGAByName(knxEvent.Kga.Name) is null)
                continue;
            sourceStates.Add(new State(knxEvent.Kga.Name, knxEvent.ValueAsString, knxEvent.ValueLabel, knxEvent.TimestampUtc));
        }
        await _azTablesSink.HousekeepingAsync(validNames, cancellationToken);

        var currentStates = await GetAllRedisState();

        var updated = 0;
        foreach (var sourceState in sourceStates)
        {
            currentStates.TryGetValue(sourceState.GroupAddress, out var currentState);
            if (currentState == sourceState)
            {
                _logger.LogTrace("{ClassName} Redis key '{Key}' is already up to date", nameof(KnxRedisStateService), sourceState.GroupAddress);
                continue;
            }
            _logger.LogDebug("{ClassName} updating Redis key '{Key}' with value '{Value}'", nameof(KnxRedisStateService), sourceState.GroupAddress, sourceState.Value);
            await SetKnxState(sourceState.GroupAddress, sourceState.TimestampUtc, sourceState.Value, sourceState.ValueLabel ?? string.Empty);
            updated++;
        }
        _logger.LogInformation("{ClassName} performed one-time sync, updated {Updated} of {Total} entries from Azure Table Storage to Redis in {ElapsedMilliseconds}ms",
            nameof(KnxRedisStateService), updated, sourceStates.Count, sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Gets all current state from Redis without triggering the sync guard.
    /// </summary>
    private async Task<Dictionary<string, State>> GetAllRedisState()
    {
        var tValues = GetHashAll(_redisConfig.GetSetting(SinkSettingKeys.SnapshotValues)!);
        var tStrings = GetHashAll(_redisConfig.GetSetting(KnxSinkKeys.SnapshotStrings)!);
        var tTimestamps = GetHashAll(_redisConfig.GetSetting(KnxSinkKeys.SnapshotTimestamps)!);

        await Task.WhenAll(tValues, tStrings, tTimestamps);

        var values = await tValues;
        var strings = await tStrings;
        var timestamps = await tTimestamps;

        var groupAddresses = values.Select(p => p.Key)
            .Union(strings.Select(p => p.Key))
            .Union(timestamps.Select(p => p.Key))
            .Distinct()
            .ToList();
        var d = new Dictionary<string, State>(groupAddresses.Count);
        foreach (var groupAddress in groupAddresses)
        {
            values.TryGetValue(groupAddress, out var value);
            strings.TryGetValue(groupAddress, out var str);
            timestamps.TryGetValue(groupAddress, out var timestamp);
            var utcDatetime = timestamp is not null
                ? new DateTime(long.Parse(timestamp), DateTimeKind.Utc)
                : DateTime.MinValue;
            d.Add(groupAddress, new State(groupAddress, value ?? string.Empty, str, utcDatetime));
        }
        return d;
    }
}
