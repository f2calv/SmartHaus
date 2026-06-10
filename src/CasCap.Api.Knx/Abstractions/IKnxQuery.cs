using CasCap.Models;

namespace CasCap.Abstractions;

/// <summary>
/// Provides query access to KNX group address event data from persistent storage.
/// </summary>
public interface IKnxQuery
{
    /// <summary>Retrieves KNX events from the backing store.</summary>
    /// <param name="id">Optional group address identifier to filter events.</param>
    /// <param name="limit">Maximum number of events to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<KnxEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default);

    /// <summary>Removes orphaned snapshot entries not present in <paramref name="validNames"/>.</summary>
    /// <param name="validNames">Set of valid group address names to retain.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HousekeepingAsync(HashSet<string> validNames, CancellationToken cancellationToken = default);
}
