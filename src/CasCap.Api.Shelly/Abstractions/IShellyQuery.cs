namespace CasCap.Abstractions;

/// <summary>
/// Provides query access to Shelly smart plug snapshot and event data.
/// </summary>
public interface IShellyQuery
{
    /// <summary>
    /// Retrieves snapshots for all known devices.
    /// </summary>
    Task<List<ShellySnapshot>> GetSnapshots();

    /// <summary>Retrieves smart plug line-item events from the primary sink.</summary>
    /// <param name="id">Optional identifier to filter events.</param>
    /// <param name="limit">Maximum number of events to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<ShellyEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default);
}
