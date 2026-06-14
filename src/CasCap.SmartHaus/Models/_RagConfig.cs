namespace CasCap.Models;

/// <summary>
/// Configuration for the RAG (Retrieval-Augmented Generation) vector storage pipeline.
/// </summary>
/// <remarks>
/// Bound from <c>CasCap:RagConfig</c>. Controls embedding generation, PDF chunking,
/// Redis vector index parameters, and the document source directory.
/// </remarks>
public record RagConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(RagConfig)}";

    /// <summary>
    /// Key into <see cref="AIConfig.Providers"/> identifying the embedding model provider.
    /// </summary>
    /// <remarks>Defaults to <c>"EdgeEmbedding"</c>.</remarks>
    [Required, MinLength(1)]
    public string EmbeddingProvider { get; init; } = "EdgeEmbedding";

    /// <summary>Redis Search index name for the document vector collection.</summary>
    /// <remarks>Defaults to <c>"rag-documents"</c>.</remarks>
    [Required, MinLength(1)]
    public string IndexName { get; init; } = "rag-documents";

    /// <summary>Embedding vector dimension. Must match the embedding model output.</summary>
    /// <remarks>Defaults to <c>768</c> (nomic-embed-text).</remarks>
    [Range(1, 8192)]
    public int Dimension { get; init; } = 768;

    /// <summary>Distance metric for vector similarity search.</summary>
    /// <remarks>Defaults to <c>"COSINE"</c>.</remarks>
    [Required, MinLength(1)]
    public string DistanceMetric { get; init; } = "COSINE";

    /// <summary>Maximum chunk size in tokens when splitting PDF text.</summary>
    /// <remarks>Defaults to <c>512</c>.</remarks>
    [Range(50, 8192)]
    public int ChunkSizeTokens { get; init; } = 512;

    /// <summary>Number of overlapping tokens between consecutive chunks.</summary>
    /// <remarks>Defaults to <c>50</c>.</remarks>
    [Range(0, 1024)]
    public int ChunkOverlapTokens { get; init; } = 50;

    /// <summary>Number of top results returned by vector similarity search.</summary>
    /// <remarks>Defaults to <c>5</c>.</remarks>
    [Range(1, 100)]
    public int TopK { get; init; } = 5;

    /// <summary>
    /// Local filesystem directory containing PDF documents to ingest on startup.
    /// </summary>
    /// <remarks>Defaults to <c>"/data/rag-documents"</c>.</remarks>
    [Required, MinLength(1)]
    public string DocumentsPath { get; init; } = "/data/rag-documents";

    /// <summary>
    /// Whether to automatically ingest documents from <see cref="DocumentsPath"/> on startup.
    /// </summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool AutoIngestOnStartup { get; init; } = true;

    /// <summary>Per-agent RAG source overrides keyed by agent name.</summary>
    /// <remarks>
    /// When an agent's key appears here, only the specified collections and TopK
    /// values apply to that agent's RAG context injection. When absent, the agent
    /// uses the default <see cref="IndexName"/> and <see cref="TopK"/>.
    /// </remarks>
    public Dictionary<string, RagSource[]> AgentSources { get; init; } = [];
}

/// <summary>
/// Identifies a vector collection and retrieval depth for agent-specific RAG context injection.
/// </summary>
public record RagSource
{
    /// <summary>Name of the vector collection to search.</summary>
    [Required, MinLength(1)]
    public required string CollectionName { get; init; }

    /// <summary>Number of top results to retrieve from this collection.</summary>
    /// <remarks>Defaults to <c>5</c>.</remarks>
    [Range(1, 100)]
    public int TopK { get; init; } = 5;
}
