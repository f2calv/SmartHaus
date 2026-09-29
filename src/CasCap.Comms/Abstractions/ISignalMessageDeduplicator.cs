namespace CasCap.Abstractions;

/// <summary>
/// Suppresses repeated processing of an inbound Signal message that Signalizr redelivers.
/// </summary>
/// <remarks>
/// <para>
/// This is duplicate <i>suppression</i>, not exactly-once delivery. The reservation is a
/// best-effort distributed hint: it can be lost if the backing store is flushed or unreachable,
/// and it cannot make processing atomic with the side effects that follow it.
/// </para>
/// <para>
/// Implementations own their storage entirely so callers never touch it directly.
/// </para>
/// </remarks>
public interface ISignalMessageDeduplicator
{
    /// <summary>Attempts to reserve exclusive processing rights for the supplied message identity.</summary>
    /// <param name="identity">The stable coordinates of the inbound message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> when this caller took the reservation and should process the message;
    /// <see langword="false"/> when a reservation already exists and the message must be skipped.
    /// </returns>
    Task<bool> TryClaimAsync(SignalMessageIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>Releases a reservation so the message may be reprocessed on redelivery.</summary>
    /// <param name="identity">The stable coordinates of the inbound message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>Called only when processing failed before any user-visible side effect occurred.</remarks>
    Task ReleaseAsync(SignalMessageIdentity identity, CancellationToken cancellationToken = default);
}
