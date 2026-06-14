namespace CasCap.Models;

/// <summary>
/// Metadata about an ingested document.
/// </summary>
public record DocumentInfo
{
    /// <summary>Stable identifier for the document (derived from the file name).</summary>
    [Description("Unique document identifier.")]
    public required string DocumentId { get; init; }

    /// <summary>Original file name of the document.</summary>
    [Description("Original PDF file name.")]
    public required string DocumentName { get; init; }

    /// <summary>Number of pages extracted from the PDF.</summary>
    [Description("Total pages in the source PDF.")]
    public int PageCount { get; init; }

    /// <summary>Number of chunks generated from the document.</summary>
    [Description("Number of text chunks stored in the vector index.")]
    public int ChunkCount { get; init; }

    /// <summary>UTC timestamp when the document was ingested.</summary>
    [Description("When the document was last ingested (UTC).")]
    public DateTime IngestedAtUtc { get; init; }
}
