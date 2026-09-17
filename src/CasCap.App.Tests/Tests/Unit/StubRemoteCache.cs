using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace CasCap.Tests.Unit;

/// <summary>
/// An <see cref="IRemoteCache"/> exposing only an inert <see cref="Db"/>; every cache convenience
/// member throws so an unexpected call fails the test loudly.
/// </summary>
public sealed class StubRemoteCache : IRemoteCache
{
    private static NotSupportedException Unused() => new("cache convenience surface is not used by these tests");

    /// <inheritdoc/>
    public IDatabase Db { get; } = StubRedisDatabase.Create();

    /// <inheritdoc/>
    public IConnectionMultiplexer Connection => throw Unused();

    /// <inheritdoc/>
    public ISubscriber Subscriber => throw Unused();

    /// <inheritdoc/>
    public IServer Server => throw Unused();

    /// <inheritdoc/>
    public ConcurrentDictionary<string, TimeSpan> SlidingExpirations { get; } = new();

    /// <inheritdoc/>
    public Dictionary<string, LoadedLuaScript> LuaScripts { get; set; } = [];

    /// <inheritdoc/>
    public string? Get(string key, CommandFlags flags = CommandFlags.None) => throw Unused();

    /// <inheritdoc/>
    public Task<string?> GetAsync(string key, CommandFlags flags = CommandFlags.None) => throw Unused();

    /// <inheritdoc/>
    public Task<byte[]?> GetBytesAsync(string key, CommandFlags flags = CommandFlags.None) => throw Unused();

    /// <inheritdoc/>
    public byte[]? GetBytes(string key, CommandFlags flags = CommandFlags.None) => throw Unused();

    /// <inheritdoc/>
    public bool Set(string key, byte[] value, TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None) => throw Unused();

    /// <inheritdoc/>
    public bool Set(string key, string value, TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None) => throw Unused();

    /// <inheritdoc/>
    public Task<bool> SetAsync(string key, byte[] value, TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None) => throw Unused();

    /// <inheritdoc/>
    public Task<bool> SetAsync(string key, string value, TimeSpan? slidingExpiration = null,
        DateTimeOffset? absoluteExpiration = null, CommandFlags flags = CommandFlags.None) => throw Unused();

    /// <inheritdoc/>
    public ValueTask<bool> ExtendSlidingExpirationAsync(string key,
        CommandFlags flags = CommandFlags.FireAndForget) => throw Unused();

    /// <inheritdoc/>
    public bool Delete(string key, CommandFlags flags = CommandFlags.None) => throw Unused();

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(string key, CommandFlags flags = CommandFlags.None) => throw Unused();

    /// <inheritdoc/>
    public Task<(TimeSpan? expiry, T? cacheEntry)> GetCacheEntryWithExpiryAsync<T>(string key,
        CommandFlags flags = CommandFlags.None, bool updateSlidingExpirationIfExists = true,
        [CallerMemberName] string caller = "") => throw Unused();

    /// <inheritdoc/>
    public LoadedLuaScript? LoadLuaScript(string scriptName, string script) => throw Unused();
}
