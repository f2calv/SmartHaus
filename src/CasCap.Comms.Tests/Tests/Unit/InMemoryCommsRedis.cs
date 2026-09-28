using StackExchange.Redis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CasCap.Tests.Unit;

/// <summary>
/// An <see cref="IRemoteCache"/> whose <see cref="Db"/> implements only the stream and string commands the
/// comms pipeline uses; every other member throws so an unexpected call fails the test loudly.
/// </summary>
public sealed class InMemoryCommsRedis : IRemoteCache
{
    private readonly ConcurrentQueue<StreamEntry> _pending = new();
    private long _nextId;

    /// <summary>Creates the fake with an in-memory <see cref="IDatabase"/>.</summary>
    public InMemoryCommsRedis()
    {
        Db = DispatchProxy.Create<IDatabase, DatabaseProxy>();
        ((DatabaseProxy)(object)Db).Owner = this;
    }

    /// <summary>Stream entry identifiers acknowledged by the consumer, in order.</summary>
    public ConcurrentQueue<string> Acknowledged { get; } = new();

    /// <summary>String values keyed by Redis key, for example cached media.</summary>
    public ConcurrentDictionary<string, byte[]> Strings { get; } = new();

    /// <summary>Keys deleted by the consumer, in order.</summary>
    public ConcurrentQueue<string> DeletedKeys { get; } = new();

    /// <inheritdoc/>
    public IDatabase Db { get; }

    /// <inheritdoc/>
    public IConnectionMultiplexer Connection => throw new NotSupportedException();

    /// <inheritdoc/>
    public ISubscriber Subscriber => throw new NotSupportedException();

    /// <inheritdoc/>
    public IServer Server => throw new NotSupportedException();

    /// <inheritdoc/>
    public ConcurrentDictionary<string, TimeSpan> SlidingExpirations { get; } = new();

    /// <inheritdoc/>
    public Dictionary<string, LoadedLuaScript> LuaScripts { get; set; } = [];

    /// <summary>Appends an entry to the comms stream.</summary>
    public void AddStreamEntry(NameValueEntry[] fields) =>
        _pending.Enqueue(new StreamEntry($"{Interlocked.Increment(ref _nextId)}-0", fields));

    /// <inheritdoc/>
    public string? Get(string key, CommandFlags flags = CommandFlags.None) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<string?> GetAsync(string key, CommandFlags flags = CommandFlags.None) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<byte[]?> GetBytesAsync(string key, CommandFlags flags = CommandFlags.None) => throw new NotSupportedException();

    /// <inheritdoc/>
    public byte[]? GetBytes(string key, CommandFlags flags = CommandFlags.None) => throw new NotSupportedException();

    /// <inheritdoc/>
    public bool Set(string key, byte[] value, TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None) => throw new NotSupportedException();

    /// <inheritdoc/>
    public bool Set(string key, string value, TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> SetAsync(string key, byte[] value, TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> SetAsync(string key, string value, TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<bool> ExtendSlidingExpirationAsync(string key, CommandFlags flags = CommandFlags.FireAndForget) =>
        throw new NotSupportedException();

    /// <inheritdoc/>
    public bool Delete(string key, CommandFlags flags = CommandFlags.None) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string key, CommandFlags flags = CommandFlags.None) => throw new NotSupportedException();

    /// <inheritdoc/>
    public Task<(TimeSpan? expiry, T? cacheEntry)> GetCacheEntryWithExpiryAsync<T>(string key,
        CommandFlags flags = CommandFlags.None, bool updateSlidingExpirationIfExists = true,
        [CallerMemberName] string caller = "") => throw new NotSupportedException();

    /// <inheritdoc/>
    public LoadedLuaScript? LoadLuaScript(string scriptName, string script) => throw new NotSupportedException();

    private StreamEntry[] ReadStream(int count)
    {
        var entries = new List<StreamEntry>(count);
        while (entries.Count < count && _pending.TryDequeue(out var entry))
            entries.Add(entry);
        return [.. entries];
    }

    private long Acknowledge(object? ids)
    {
        RedisValue[] values = ids switch
        {
            RedisValue single => [single],
            RedisValue[] many => many,
            _ => [],
        };
        foreach (var value in values)
            Acknowledged.Enqueue(value.ToString());
        return values.Length;
    }

    private bool DeleteKey(RedisKey key)
    {
        DeletedKeys.Enqueue(key.ToString());
        return Strings.TryRemove(key.ToString(), out _);
    }

    private bool SetString(RedisKey key, RedisValue value)
    {
        Strings[key.ToString()] = (byte[])value!;
        return true;
    }

    /// <summary>Routes the handful of <see cref="IDatabase"/> commands the comms pipeline uses to the owning fake.</summary>
    private class DatabaseProxy : DispatchProxy
    {
        public InMemoryCommsRedis? Owner { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var owner = Owner ?? throw new InvalidOperationException("The proxy has no owner.");
            return (targetMethod?.Name, args) switch
            {
                (nameof(IDatabase.StreamCreateConsumerGroupAsync), _) => Task.FromResult(true),
                (nameof(IDatabase.StreamReadGroupAsync), [_, _, _, _, var count, ..]) =>
                    Task.FromResult(owner.ReadStream((int?)count ?? 10)),
                (nameof(IDatabase.StreamAcknowledgeAsync), [_, _, var ids, ..]) =>
                    Task.FromResult(owner.Acknowledge(ids)),
                (nameof(IDatabase.StringGetAsync), [RedisKey key, ..]) =>
                    Task.FromResult(owner.Strings.TryGetValue(key.ToString(), out var value) ? (RedisValue)value : RedisValue.Null),
                (nameof(IDatabase.StringSetAsync), [RedisKey key, RedisValue value, ..]) =>
                    Task.FromResult(owner.SetString(key, value)),
                (nameof(IDatabase.KeyDeleteAsync), [RedisKey key, ..]) => Task.FromResult(owner.DeleteKey(key)),
                _ => throw new NotSupportedException(targetMethod?.Name),
            };
        }
    }
}
