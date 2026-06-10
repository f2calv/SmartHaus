namespace CasCap.Abstractions;

/// <summary>
/// Provides query access to edge hardware snapshot data from persistent storage.
/// </summary>
public interface IEdgeHardwareQuery
{
    /// <summary>
    /// Retrieves the latest edge hardware snapshot for every known node.
    /// </summary>
    Task<List<EdgeHardwareSnapshot>> GetSnapshots();

    /// <summary>Retrieves edge hardware line-item events from the primary sink.</summary>
    /// <param name="id">Optional identifier to filter events.</param>
    /// <param name="limit">Maximum number of events to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<EdgeHardwareEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default);
}
