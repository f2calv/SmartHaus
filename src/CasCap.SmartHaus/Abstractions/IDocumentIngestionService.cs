namespace CasCap.Abstractions;

/// <summary>
/// Abstraction for ingesting documents (PDFs) into the vector store.
/// </summary>
public interface IDocumentIngestionService
{
    /// <summary>
    /// Ingests a PDF document: extracts text, chunks, generates embeddings, and stores in the vector index.
    /// </summary>
    /// <param name="pdfStream">Stream containing the PDF content.</param>
    /// <param name="documentName">Human-readable document name.</param>
    /// <param name="collectionName">Target vector collection name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata about the ingested document.</returns>
    Task<DocumentInfo> IngestDocumentAsync(Stream pdfStream, string documentName, string collectionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingests all PDF files from a directory into the vector store.
    /// </summary>
    /// <param name="directoryPath">Absolute path to the directory containing PDF files.</param>
    /// <param name="collectionName">Target vector collection name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata for each ingested document.</returns>
    Task<List<DocumentInfo>> IngestDirectoryAsync(string directoryPath, string collectionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a document and all its chunks from the vector store.
    /// </summary>
    /// <param name="documentId">The document identifier to remove.</param>
    /// <param name="collectionName">Target vector collection name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveDocumentAsync(string documentId, string collectionName, CancellationToken cancellationToken = default);
}
