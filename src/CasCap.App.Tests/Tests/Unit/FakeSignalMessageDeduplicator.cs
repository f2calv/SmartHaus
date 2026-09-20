using CasCap.Abstractions;
using System.Collections.Concurrent;

namespace CasCap.Tests.Unit;

/// <summary>
/// <see cref="ISignalMessageDeduplicator"/> substitute with a configurable claim outcome that
/// records every reservation and release.
/// </summary>
public sealed class FakeSignalMessageDeduplicator : ISignalMessageDeduplicator
{
    /// <summary>Whether the next claim succeeds.</summary>
    public bool ClaimResult { get; set; } = true;

    /// <summary>Every identity presented to <see cref="TryClaimAsync"/>, in order.</summary>
    public ConcurrentQueue<SignalMessageIdentity> Claims { get; } = new();

    /// <summary>Every identity presented to <see cref="ReleaseAsync"/>, in order.</summary>
    public ConcurrentQueue<SignalMessageIdentity> Releases { get; } = new();

    /// <inheritdoc/>
    public Task<bool> TryClaimAsync(SignalMessageIdentity identity, CancellationToken cancellationToken = default)
    {
        Claims.Enqueue(identity);
        return Task.FromResult(ClaimResult);
    }

    /// <inheritdoc/>
    public Task ReleaseAsync(SignalMessageIdentity identity, CancellationToken cancellationToken = default)
    {
        Releases.Enqueue(identity);
        return Task.CompletedTask;
    }
}
