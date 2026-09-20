namespace CasCap.Models;

/// <summary>The outcome of deleting every attachment carried by one inbound Signal envelope.</summary>
public sealed record AttachmentCleanupResult
{
    /// <summary>The attachment identifiers confirmed absent from the signal-cli attachment store.</summary>
    public required IReadOnlyList<string> Deleted { get; init; }

    /// <summary>The attachment identifiers still present after every permitted attempt.</summary>
    public required IReadOnlyList<string> Remaining { get; init; }

    /// <summary>Whether every supplied attachment identifier was removed.</summary>
    public bool Complete => Remaining.Count == 0;
}
