namespace CasCap.Services;

/// <summary>
/// Thread-safe queue backed by a <see cref="ConcurrentQueue{T}"/> for KNX state change
/// operations that are processed out-of-band by <see cref="KnxAutomationBgService"/>.
/// </summary>
public class StateChangeQueue : IStateChangeQueue
{
    private readonly ConcurrentQueue<KnxStateChangeItem> _queue = new();

    /// <inheritdoc/>
    public void Enqueue(KnxStateChangeItem item) => _queue.Enqueue(item);

    /// <inheritdoc/>
    public bool TryDequeue([NotNullWhen(true)] out KnxStateChangeItem? item) => _queue.TryDequeue(out item);

    /// <inheritdoc/>
    public int Count => _queue.Count;
}
