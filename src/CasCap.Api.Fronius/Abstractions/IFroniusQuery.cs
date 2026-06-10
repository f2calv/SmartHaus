namespace CasCap.Abstractions;

/// <summary>
/// Provides query access to Fronius inverter snapshot and event data.
/// </summary>
public interface IFroniusQuery
{
    /// <summary>
    /// Retrieves the latest solar inverter snapshot.
    /// </summary>
    Task<InverterSnapshot> GetSnapshot();

    /// <summary>Retrieves inverter line-item events from the primary sink.</summary>
    /// <param name="id">Optional identifier to filter events.</param>
    /// <param name="limit">Maximum number of events to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<FroniusEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default);
}
