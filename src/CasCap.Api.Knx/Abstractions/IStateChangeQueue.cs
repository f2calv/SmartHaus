namespace CasCap.Abstractions;

/// <summary>
/// Abstracts a thread-safe queue for KNX state change operations that are
/// processed out-of-band by a background service.
/// </summary>
public interface IStateChangeQueue
{
    /// <summary>
    /// Enqueues a <see cref="KnxStateChangeItem"/> for background processing.
    /// </summary>
    /// <param name="item">The state change item to enqueue.</param>
    void Enqueue(KnxStateChangeItem item);

    /// <summary>
    /// Attempts to dequeue the next <see cref="KnxStateChangeItem"/>.
    /// </summary>
    /// <param name="item">The dequeued item, or <see langword="null"/> if the queue is empty.</param>
    /// <returns><see langword="true"/> if an item was dequeued; otherwise <see langword="false"/>.</returns>
    bool TryDequeue([NotNullWhen(true)] out KnxStateChangeItem? item);

    /// <summary>
    /// Gets the number of items currently in the queue.
    /// </summary>
    int Count { get; }
}
