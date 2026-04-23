namespace CasCap.Abstractions;

/// <summary>
/// Abstracts KNX group address state operations backed by a persistent store.
/// </summary>
public interface IKnxState
{
    /// <summary>
    /// Writes a KNX group address state update to the backing store and appends to event streams.
    /// </summary>
    /// <param name="groupAddressName">The KNX group address name.</param>
    /// <param name="timestampUtc">The UTC timestamp of the event.</param>
    /// <param name="valueDecoded">The decoded value as a string.</param>
    /// <param name="valueLabelDecoded">The decoded human-readable value label.</param>
    Task SetKnxState(string groupAddressName, DateTime timestampUtc, string valueDecoded, string? valueLabelDecoded);

    /// <summary>
    /// Retrieves the <see cref="State"/> for a given group address from the backing store.
    /// </summary>
    /// <param name="groupAddressName">The KNX group address name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <see cref="State"/> if found; otherwise <see langword="null"/>.</returns>
    Task<State?> GetKnxState(string groupAddressName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all <see cref="State"/> entries from the backing store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary keyed by group address name.</returns>
    Task<Dictionary<string, State>> GetAllState(CancellationToken cancellationToken = default);
}
