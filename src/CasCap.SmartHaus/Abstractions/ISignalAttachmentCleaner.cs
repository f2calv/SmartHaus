namespace CasCap.Abstractions;

/// <summary>
/// Deletes inbound Signal attachments from the signal-cli attachment store once they are no longer needed.
/// </summary>
/// <remarks>
/// signal-cli retains every received attachment on disk until it is explicitly deleted, so cleanup
/// runs for all attachments on an envelope — including any the receive path chose not to read.
/// </remarks>
public interface ISignalAttachmentCleaner
{
    /// <summary>Deletes each supplied attachment identifier, retrying bounded transient failures.</summary>
    /// <param name="attachmentIds">The attachment identifiers carried by one envelope.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Which identifiers were removed and which remain.</returns>
    Task<AttachmentCleanupResult> DeleteAllAsync(IReadOnlyList<string> attachmentIds, CancellationToken cancellationToken = default);
}
