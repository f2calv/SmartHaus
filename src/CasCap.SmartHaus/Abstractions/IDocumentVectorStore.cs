namespace CasCap.Abstractions;

/// <summary>
/// Abstraction for storing and searching document chunks in a vector database.
/// </summary>
public interface IDocumentVectorStore
{
    /// <summary>Ensures the vector collection and its index exist in Redis.</summary>
    /// <param name="collectionName">Logical name of the collection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnsureCollectionAsync(string collectionName, CancellationToken cancellationToken = default);

    /// <summary>Upserts pre-embedded document chunks into the vector store.</summary>
    /// <param name="collectionName">Logical name of the collection.</param>
    /// <param name="chunks">Chunks with embeddings already populated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertChunksAsync(string collectionName, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>Performs a vector similarity search using the provided query embedding.</summary>
    /// <param name="collectionName">Logical name of the collection.</param>
    /// <param name="queryEmbedding">The query vector.</param>
    /// <param name="topK">Number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ranked search results.</returns>
    Task<List<DocumentSearchResult>> SearchAsync(string collectionName, ReadOnlyMemory<float> queryEmbedding, int topK, CancellationToken cancellationToken = default);

    /// <summary>Removes all chunks belonging to a specific document.</summary>
    /// <param name="collectionName">Logical name of the collection.</param>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveDocumentAsync(string collectionName, string documentId, CancellationToken cancellationToken = default);

    /// <summary>Deletes the entire collection and its index.</summary>
    /// <param name="collectionName">Logical name of the collection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default);

    /// <summary>Returns metadata for all ingested documents in a collection.</summary>
    /// <param name="collectionName">Logical name of the collection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<List<DocumentInfo>> ListDocumentsAsync(string collectionName, CancellationToken cancellationToken = default);
}
