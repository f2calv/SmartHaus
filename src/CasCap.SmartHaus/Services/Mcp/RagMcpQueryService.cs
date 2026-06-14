using Microsoft.Extensions.AI;

namespace CasCap.Services;

/// <summary>
/// MCP tools for managing RAG document collections — searching, listing, and ingesting documents.
/// </summary>
[McpServerToolType]
public class RagMcpQueryService(
    ILogger<RagMcpQueryService> logger,
    IDocumentVectorStore vectorStore,
    IDocumentIngestionService ingestionSvc,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    IOptions<RagConfig> ragConfig)
{
    /// <summary>
    /// Searches ingested documents for content relevant to the given query using vector similarity.
    /// </summary>
    [McpServerTool]
    [Description("Semantic search across ingested PDF documents. Returns the most relevant text passages.")]
    public async Task<List<DocumentSearchResult>> SearchDocuments(
        [Description("Natural-language search query.")]
        string query,
        [Description("Collection name to search. Omit for the default collection.")]
        string? collectionName = null,
        [Description("Number of results to return (1–100). Defaults to the configured TopK.")]
        int? topK = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} {MethodName} query={Query}, collection={CollectionName}",
            nameof(RagMcpQueryService), nameof(SearchDocuments), query, collectionName);

        var config = ragConfig.Value;
        collectionName ??= config.IndexName;
        topK ??= config.TopK;

        var embeddingResult = await embeddingGenerator.GenerateAsync(query, cancellationToken: cancellationToken);
        return await vectorStore.SearchAsync(collectionName, embeddingResult.Vector, topK.Value, cancellationToken);
    }

    /// <summary>
    /// Lists all available document collections with their document counts.
    /// </summary>
    [McpServerTool]
    [Description("Lists all RAG document collections available for search.")]
    public async Task<List<DocumentInfo>> ListDocuments(
        [Description("Collection name. Omit for the default collection.")]
        string? collectionName = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} {MethodName} collection={CollectionName}",
            nameof(RagMcpQueryService), nameof(ListDocuments), collectionName);

        var config = ragConfig.Value;
        collectionName ??= config.IndexName;

        return await vectorStore.ListDocumentsAsync(collectionName, cancellationToken);
    }

    /// <summary>
    /// Triggers ingestion of a PDF document from the configured documents directory.
    /// </summary>
    [McpServerTool]
    [Description("Ingests a PDF file from the documents directory into the vector store for RAG search.")]
    public async Task<DocumentInfo> IngestDocument(
        [Description("PDF file name (relative to the documents directory).")]
        string fileName,
        [Description("Collection name. Omit for the default collection.")]
        string? collectionName = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} {MethodName} file={FileName}, collection={CollectionName}",
            nameof(RagMcpQueryService), nameof(IngestDocument), fileName, collectionName);

        var config = ragConfig.Value;
        collectionName ??= config.IndexName;

        var filePath = Path.Combine(config.DocumentsPath, fileName);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"PDF file not found: {fileName}", filePath);

        await using var stream = File.OpenRead(filePath);
        return await ingestionSvc.IngestDocumentAsync(stream, fileName, collectionName, cancellationToken);
    }

    /// <summary>
    /// Returns metadata about a specific ingested document.
    /// </summary>
    [McpServerTool]
    [Description("Gets metadata about a specific ingested document — page count, chunk count, ingestion time.")]
    public async Task<DocumentInfo?> GetDocumentInfo(
        [Description("Document identifier (derived from file name, e.g. 'my-manual-pdf').")]
        string documentId,
        [Description("Collection name. Omit for the default collection.")]
        string? collectionName = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} {MethodName} documentId={DocumentId}, collection={CollectionName}",
            nameof(RagMcpQueryService), nameof(GetDocumentInfo), documentId, collectionName);

        var config = ragConfig.Value;
        collectionName ??= config.IndexName;

        var documents = await vectorStore.ListDocumentsAsync(collectionName, cancellationToken);
        return documents.Find(d => string.Equals(d.DocumentId, documentId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Removes a document and all its chunks from the vector store.
    /// </summary>
    [McpServerTool]
    [Description("Removes a document and all its chunks from the vector store.")]
    public async Task<bool> RemoveDocument(
        [Description("Document identifier to remove.")]
        string documentId,
        [Description("Collection name. Omit for the default collection.")]
        string? collectionName = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug("{ClassName} {MethodName} documentId={DocumentId}, collection={CollectionName}",
            nameof(RagMcpQueryService), nameof(RemoveDocument), documentId, collectionName);

        var config = ragConfig.Value;
        collectionName ??= config.IndexName;

        await ingestionSvc.RemoveDocumentAsync(documentId, collectionName, cancellationToken);
        return true;
    }
}
