namespace CasCap.Abstractions;

/// <summary>
/// Provides query access to Buderus heat pump snapshot and event data.
/// </summary>
public interface IBuderusQuery
{
    /// <summary>
    /// Retrieves a typed snapshot of key Buderus heat pump sensor values.
    /// </summary>
    Task<BuderusSnapshot> GetSnapshot();

    /// <summary>Retrieves events, optionally filtered by sensor <paramref name="id"/>.</summary>
    /// <param name="id">Optional sensor identifier to filter events.</param>
    /// <param name="limit">Maximum number of events to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<BuderusEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default);
}
