namespace CasCap.Models;

/// <summary>
/// A single result from a vector similarity search against ingested documents.
/// </summary>
public record DocumentSearchResult
{
    /// <summary>Human-readable document name.</summary>
    [Description("Source document name.")]
    public required string DocumentName { get; init; }

    /// <summary>One-based page number in the source PDF.</summary>
    [Description("Page number in the source PDF.")]
    public int PageNumber { get; init; }

    /// <summary>Similarity score (lower is more similar for cosine distance).</summary>
    [Description("Similarity score — lower values indicate higher relevance.")]
    public double Score { get; init; }

    /// <summary>The text content of the matched chunk.</summary>
    [Description("Matched text content from the document.")]
    public required string Content { get; init; }
}
