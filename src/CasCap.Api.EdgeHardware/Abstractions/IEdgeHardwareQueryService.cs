namespace CasCap.Abstractions;

/// <summary>
/// Defines the public query operations exposed by the edge hardware service.
/// </summary>
public interface IEdgeHardwareQueryService
{
    /// <summary>
    /// Retrieves the latest edge hardware snapshots for all nodes from the primary sink.
    /// </summary>
    Task<List<EdgeHardwareSnapshot>> GetLatestSnapshots();

    /// <summary>
    /// Retrieves recent edge hardware events from the primary sink.
    /// </summary>
    /// <param name="limit">Maximum number of events to return. Default 100, maximum 1000.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<EdgeHardwareEvent> GetEvents(int limit = 100, CancellationToken cancellationToken = default);
}
