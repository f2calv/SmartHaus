using System.Runtime.InteropServices;
using StackExchange.Redis;

namespace CasCap.Services;

/// <summary>
/// Redis-backed implementation of <see cref="IDocumentVectorStore"/> using Redis Search
/// (<c>FT.CREATE</c>, <c>FT.SEARCH</c>) commands via <see cref="StackExchange.Redis"/>.
/// </summary>
/// <remarks>
/// Each collection maps to a Redis Search index. Document chunks are stored as hashes
/// with keys following the pattern <c>rag:{collectionName}:{chunkId}</c>.
/// Document metadata is stored as a separate hash at <c>rag:meta:{collectionName}:{documentId}</c>.
/// </remarks>
public class RedisDocumentVectorStore(
    ILogger<RedisDocumentVectorStore> logger,
    IConnectionMultiplexer connectionMultiplexer,
    IOptions<RagConfig> ragConfig
    ) : IDocumentVectorStore
{
    private const string KeyPrefix = "rag";
    private const string MetaPrefix = "rag:meta";

    private IDatabase Db => connectionMultiplexer.GetDatabase();

    private static string ChunkKey(string collectionName, string chunkId) =>
        $"{KeyPrefix}:{collectionName}:{chunkId}";

    private static string IndexName(string collectionName) =>
        $"{KeyPrefix}:{collectionName}:idx";

    /// <inheritdoc/>
    public async Task EnsureCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        var db = Db;
        var idxName = IndexName(collectionName);
        var config = ragConfig.Value;

        try
        {
            // Check if index already exists.
            await db.ExecuteAsync("FT.INFO", idxName);
            logger.LogDebug("{ClassName} index {IndexName} already exists", nameof(RedisDocumentVectorStore), idxName);
            return;
        }
        catch (RedisServerException ex) when (ex.Message.Contains("Unknown index name", StringComparison.OrdinalIgnoreCase))
        {
            // Index doesn't exist — create it.
        }

        var prefix = $"{KeyPrefix}:{collectionName}:";
        await db.ExecuteAsync("FT.CREATE", idxName,
            "ON", "HASH",
            "PREFIX", "1", prefix,
            "SCHEMA",
            "DocumentId", "TAG",
            "DocumentName", "TEXT",
            "Content", "TEXT",
            "PageNumber", "NUMERIC",
            "ChunkIndex", "NUMERIC",
            "Embedding", "VECTOR", "HNSW", "6",
                "TYPE", "FLOAT32",
                "DIM", config.Dimension.ToString(),
                "DISTANCE_METRIC", config.DistanceMetric);

        logger.LogInformation("{ClassName} created index {IndexName} with dimension {Dimension}",
            nameof(RedisDocumentVectorStore), idxName, config.Dimension);
    }

    /// <inheritdoc/>
    public async Task UpsertChunksAsync(string collectionName, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        var db = Db;

        foreach (var chunk in chunks)
        {
            var key = ChunkKey(collectionName, chunk.Id);
            var embeddingBytes = EmbeddingToBytes(chunk.Embedding);

            await db.HashSetAsync(key,
            [
                new HashEntry("DocumentId", chunk.DocumentId),
                new HashEntry("DocumentName", chunk.DocumentName),
                new HashEntry("Content", chunk.Content),
                new HashEntry("PageNumber", chunk.PageNumber),
                new HashEntry("ChunkIndex", chunk.ChunkIndex),
                new HashEntry("Embedding", embeddingBytes),
            ]);
        }

        logger.LogInformation("{ClassName} upserted {Count} chunks into {CollectionName}",
            nameof(RedisDocumentVectorStore), chunks.Count, collectionName);
    }

    /// <inheritdoc/>
    public async Task<List<DocumentSearchResult>> SearchAsync(string collectionName, ReadOnlyMemory<float> queryEmbedding, int topK, CancellationToken cancellationToken = default)
    {
        var db = Db;
        var idxName = IndexName(collectionName);
        var queryBytes = EmbeddingToBytes(queryEmbedding);

        // KNN vector search query.
        var query = $"*=>[KNN {topK} @Embedding $vector AS score]";

        var result = await db.ExecuteAsync("FT.SEARCH", idxName, query,
            "PARAMS", "2", "vector", queryBytes,
            "SORTBY", "score",
            "RETURN", "5", "DocumentName", "Content", "PageNumber", "score", "DocumentId",
            "DIALECT", "2");

        var results = new List<DocumentSearchResult>();
        var array = (RedisResult[])result!;

        // First element is total count, then alternating key/value pairs.
        for (var i = 1; i < array.Length; i += 2)
        {
            if (i + 1 >= array.Length)
                break;

            var fields = (RedisResult[])array[i + 1]!;
            var fieldDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var j = 0; j < fields.Length; j += 2)
                fieldDict[fields[j].ToString()!] = fields[j + 1].ToString()!;

            results.Add(new DocumentSearchResult
            {
                DocumentName = fieldDict.GetValueOrDefault("DocumentName") ?? string.Empty,
                Content = fieldDict.GetValueOrDefault("Content") ?? string.Empty,
                PageNumber = int.TryParse(fieldDict.GetValueOrDefault("PageNumber"), out var pn) ? pn : 0,
                Score = double.TryParse(fieldDict.GetValueOrDefault("score"), out var s) ? s : 0,
            });
        }

        logger.LogDebug("{ClassName} search in {CollectionName} returned {Count} results",
            nameof(RedisDocumentVectorStore), collectionName, results.Count);

        return results;
    }

    /// <inheritdoc/>
    public async Task RemoveDocumentAsync(string collectionName, string documentId, CancellationToken cancellationToken = default)
    {
        var db = Db;
        var server = connectionMultiplexer.GetServers().First();

        // Find and delete all chunk keys belonging to this document.
        var keyPattern = $"{KeyPrefix}:{collectionName}:{documentId}:*";
        var deletedCount = 0;

        await foreach (var key in server.KeysAsync(pattern: keyPattern))
        {
            await db.KeyDeleteAsync(key);
            deletedCount++;
        }

        // Remove document metadata.
        var metaKey = $"{MetaPrefix}:{collectionName}:{documentId}";
        await db.KeyDeleteAsync(metaKey);

        logger.LogInformation("{ClassName} removed {Count} chunks for document {DocumentId} from {CollectionName}",
            nameof(RedisDocumentVectorStore), deletedCount, documentId, collectionName);
    }

    /// <inheritdoc/>
    public async Task DeleteCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        var db = Db;
        var server = connectionMultiplexer.GetServers().First();
        var idxName = IndexName(collectionName);

        // Drop the index (DD = delete associated docs).
        try
        {
            await db.ExecuteAsync("FT.DROPINDEX", idxName, "DD");
        }
        catch (RedisServerException ex) when (ex.Message.Contains("Unknown index name", StringComparison.OrdinalIgnoreCase))
        {
            // Already gone.
        }

        // Also clean up metadata keys.
        await foreach (var key in server.KeysAsync(pattern: $"{MetaPrefix}:{collectionName}:*"))
            await db.KeyDeleteAsync(key);

        logger.LogInformation("{ClassName} deleted collection {CollectionName}", nameof(RedisDocumentVectorStore), collectionName);
    }

    /// <inheritdoc/>
    public async Task<List<DocumentInfo>> ListDocumentsAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        var db = Db;
        var server = connectionMultiplexer.GetServers().First();
        var documents = new List<DocumentInfo>();

        await foreach (var key in server.KeysAsync(pattern: $"{MetaPrefix}:{collectionName}:*"))
        {
            var hash = await db.HashGetAllAsync(key);
            if (hash.Length == 0)
                continue;

            var fields = hash.ToDictionary(h => h.Name.ToString(), h => h.Value.ToString());
            documents.Add(new DocumentInfo
            {
                DocumentId = fields.GetValueOrDefault("DocumentId") ?? string.Empty,
                DocumentName = fields.GetValueOrDefault("DocumentName") ?? string.Empty,
                PageCount = int.TryParse(fields.GetValueOrDefault("PageCount"), out var pc) ? pc : 0,
                ChunkCount = int.TryParse(fields.GetValueOrDefault("ChunkCount"), out var cc) ? cc : 0,
                IngestedAtUtc = DateTime.TryParse(fields.GetValueOrDefault("IngestedAtUtc"), out var dt) ? dt : DateTime.MinValue,
            });
        }

        return documents;
    }

    /// <summary>Stores document metadata as a Redis hash.</summary>
    internal async Task StoreDocumentMetadataAsync(string collectionName, DocumentInfo info)
    {
        var db = Db;
        var metaKey = $"{MetaPrefix}:{collectionName}:{info.DocumentId}";

        await db.HashSetAsync(metaKey,
        [
            new HashEntry("DocumentId", info.DocumentId),
            new HashEntry("DocumentName", info.DocumentName),
            new HashEntry("PageCount", info.PageCount),
            new HashEntry("ChunkCount", info.ChunkCount),
            new HashEntry("IngestedAtUtc", info.IngestedAtUtc.ToString("O")),
        ]);
    }

    /// <summary>Converts a float embedding to its binary representation for Redis vector storage.</summary>
    private static byte[] EmbeddingToBytes(ReadOnlyMemory<float> embedding)
    {
        var span = embedding.Span;
        var bytes = new byte[span.Length * sizeof(float)];
        MemoryMarshal.AsBytes(span).CopyTo(bytes);
        return bytes;
    }
}
