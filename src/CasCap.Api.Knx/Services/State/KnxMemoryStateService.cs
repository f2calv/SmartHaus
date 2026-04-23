namespace CasCap.Services;

/// <summary>
/// In-memory implementation of <see cref="IKnxState"/> backed by a <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// Intended for local development or as a fallback when Redis is unavailable.
/// </summary>
public class KnxMemoryStateService(ILogger<KnxMemoryStateService> logger) : IKnxState
{
    private readonly ConcurrentDictionary<string, State> _states = new();

    /// <inheritdoc/>
    public Task SetKnxState(string groupAddressName, DateTime timestamp, string value, string? valueLabel)
    {
        var state = new State(groupAddressName, value, valueLabel, timestamp);
        _states.AddOrUpdate(groupAddressName, state, (_, _) => state);
        logger.LogTrace("{ClassName} set state for '{GroupAddressName}'", nameof(KnxMemoryStateService), groupAddressName);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<State?> GetKnxState(string groupAddressName, CancellationToken cancellationToken = default)
    {
        _states.TryGetValue(groupAddressName, out var state);
        if (state is null)
            logger.LogDebug("{ClassName} no state found for '{GroupAddressName}'", nameof(KnxMemoryStateService), groupAddressName);
        return Task.FromResult(state);
    }

    /// <inheritdoc/>
    public Task<Dictionary<string, State>> GetAllState(CancellationToken cancellationToken = default)
        => Task.FromResult(new Dictionary<string, State>(_states));
}
