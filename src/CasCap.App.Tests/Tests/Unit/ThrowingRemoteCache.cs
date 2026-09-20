using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace CasCap.Tests.Unit;

/// <summary>
/// An <see cref="IRemoteCache"/> whose every member throws, used to prove that callers degrade
/// safely when the cache is unreachable.
/// </summary>
public sealed class ThrowingRemoteCache : IRemoteCache
{
    //The concrete type is irrelevant; the production code catches any non-cancellation exception.
    private static TimeoutException Unreachable() => new("cache unreachable");

    /// <inheritdoc/>
    public IDatabase Db => throw Unreachable();

    /// <inheritdoc/>
    public IConnectionMultiplexer Connection => throw Unreachable();

    /// <inheritdoc/>
    public ISubscriber Subscriber => throw Unreachable();

    /// <inheritdoc/>
    public IServer Server => throw Unreachable();

    /// <inheritdoc/>
    public ConcurrentDictionary<string, TimeSpan> SlidingExpirations { get; } = new();

    /// <inheritdoc/>
    public Dictionary<string, LoadedLuaScript> LuaScripts { get; set; } = [];

    /// <inheritdoc/>
    public string? Get(string key, CommandFlags flags = CommandFlags.None) => throw Unreachable();

    /// <inheritdoc/>
    public Task<string?> GetAsync(string key, CommandFlags flags = CommandFlags.None) => throw Unreachable();

    /// <inheritdoc/>
    public Task<byte[]?> GetBytesAsync(string key, CommandFlags flags = CommandFlags.None) => throw Unreachable();

    /// <inheritdoc/>
    public byte[]? GetBytes(string key, CommandFlags flags = CommandFlags.None) => throw Unreachable();

    /// <inheritdoc/>
    public bool Set(string key, byte[] value, TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None) => throw Unreachable();

    /// <inheritdoc/>
    public bool Set(string key, string value, TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None) => throw Unreachable();

    /// <inheritdoc/>
    public Task<bool> SetAsync(string key, byte[] value, TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None) => throw Unreachable();

    /// <inheritdoc/>
    public Task<bool> SetAsync(string key, string value, TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None) => throw Unreachable();

    /// <inheritdoc/>
    public ValueTask<bool> ExtendSlidingExpirationAsync(string key,
        CommandFlags flags = CommandFlags.FireAndForget) => throw Unreachable();

    /// <inheritdoc/>
    public bool Delete(string key, CommandFlags flags = CommandFlags.None) => throw Unreachable();

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string key, CommandFlags flags = CommandFlags.None) => throw Unreachable();

    /// <inheritdoc/>
    public Task<(TimeSpan? expiry, T? cacheEntry)> GetCacheEntryWithExpiryAsync<T>(string key,
        CommandFlags flags = CommandFlags.None, bool updateSlidingExpirationIfExists = true,
        [CallerMemberName] string caller = "") => throw Unreachable();

    /// <inheritdoc/>
    public LoadedLuaScript? LoadLuaScript(string scriptName, string script) => throw Unreachable();
}
