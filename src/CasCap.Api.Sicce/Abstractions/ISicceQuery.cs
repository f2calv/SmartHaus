namespace CasCap.Abstractions;

/// <summary>
/// Provides query access to Sicce water pump snapshot data.
/// </summary>
public interface ISicceQuery
{
    /// <summary>
    /// Retrieves the latest Sicce device snapshot.
    /// </summary>
    Task<SicceSnapshot> GetSnapshot();

    /// <summary>Retrieves Sicce pump line-item events from the primary sink.</summary>
    /// <param name="id">Optional identifier to filter events.</param>
    /// <param name="limit">Maximum number of events to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<SicceEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default);
}
