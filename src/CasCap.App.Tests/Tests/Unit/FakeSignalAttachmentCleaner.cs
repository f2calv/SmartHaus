using CasCap.Abstractions;
using System.Collections.Concurrent;

namespace CasCap.Tests.Unit;

/// <summary>
/// <see cref="ISignalAttachmentCleaner"/> substitute recording the identifiers presented for
/// deletion and returning a configurable completeness result.
/// </summary>
public sealed class FakeSignalAttachmentCleaner : ISignalAttachmentCleaner
{
    /// <summary>Each call's identifier list, in order.</summary>
    public ConcurrentQueue<IReadOnlyList<string>> Calls { get; } = new();

    /// <summary>Whether cleanup reports every identifier removed.</summary>
    public bool Complete { get; set; } = true;

    /// <summary>The identifiers presented by the most recent call.</summary>
    public IReadOnlyList<string> LastCall => Calls.LastOrDefault() ?? [];

    /// <inheritdoc/>
    public Task<AttachmentCleanupResult> DeleteAllAsync(IReadOnlyList<string> attachmentIds,
        CancellationToken cancellationToken = default)
    {
        List<string> ids = [.. attachmentIds];
        Calls.Enqueue(ids);
        return Task.FromResult(Complete
            ? new AttachmentCleanupResult { Deleted = ids, Remaining = [] }
            : new AttachmentCleanupResult { Deleted = [], Remaining = ids });
    }
}
