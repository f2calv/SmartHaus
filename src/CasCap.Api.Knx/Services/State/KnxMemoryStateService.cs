namespace CasCap.Services;

/// <summary>
/// In-memory implementation of <see cref="IKnxState"/> backed by a <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// Intended for local development or as a fallback when Redis is unavailable.
/// </summary>
public sealed class KnxMemoryStateService(ILogger<KnxMemoryStateService> logger) : IKnxState
{
    private readonly ConcurrentDictionary<string, State> _states = new();

    /// <inheritdoc/>
    public ValueTask SetKnxState(string groupAddressName, DateTime timestamp, string value, string? valueLabel)
    {
        var state = new State(groupAddressName, value, valueLabel, timestamp);
        _states.AddOrUpdate(groupAddressName, state, (_, _) => state);
        logger.LogTrace("{ClassName} set state for '{GroupAddressName}'", nameof(KnxMemoryStateService), groupAddressName);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<State?> GetKnxState(string groupAddressName, CancellationToken cancellationToken = default)
    {
        _states.TryGetValue(groupAddressName, out var state);
        if (state is null)
            logger.LogDebug("{ClassName} no state found for '{GroupAddressName}'", nameof(KnxMemoryStateService), groupAddressName);
        return ValueTask.FromResult(state);
    }

    /// <inheritdoc/>
    public ValueTask<Dictionary<string, State>> GetAllState(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new Dictionary<string, State>(_states));
}
