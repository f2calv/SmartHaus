using System.Collections.Concurrent;

namespace CasCap.Tests.Unit;

/// <summary>Volatile <see cref="ISessionStore"/> substitute so agent sessions never touch Redis.</summary>
public sealed class FakeSessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<string, string> _sessions = new();

    /// <inheritdoc/>
    public ValueTask<string?> GetAsync(string key) =>
        new(_sessions.TryGetValue(key, out var json) ? json : null);

    /// <inheritdoc/>
    public ValueTask SetAsync(string key, string json, TimeSpan? slidingExpiration = null)
    {
        _sessions[key] = json;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DeleteAsync(string key)
    {
        _sessions.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }
}
