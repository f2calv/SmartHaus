namespace CasCap.Models;

/// <summary>
/// A chunked section of an ingested document with its embedding vector, stored in Redis
/// as a hash with a vector field for similarity search.
/// </summary>
public class DocumentChunk
{
    /// <summary>Unique chunk identifier (<c>{documentId}:{chunkIndex}</c>).</summary>
    public required string Id { get; init; }

    /// <summary>Stable identifier for the source document (derived from the file name).</summary>
    public required string DocumentId { get; init; }

    /// <summary>Human-readable document name (e.g. the original PDF filename).</summary>
    public required string DocumentName { get; init; }

    /// <summary>Plain-text content of this chunk.</summary>
    public required string Content { get; init; }

    /// <summary>One-based page number in the source PDF where this chunk starts.</summary>
    public int PageNumber { get; init; }

    /// <summary>Zero-based index of this chunk within its parent document.</summary>
    public int ChunkIndex { get; init; }

    /// <summary>Embedding vector generated from <see cref="Content"/>.</summary>
    public ReadOnlyMemory<float> Embedding { get; set; }
}
