using Microsoft.Extensions.VectorData;
using StackExchange.Redis;

namespace CasCap.Services;

/// <summary>
/// Redis-backed implementation of <see cref="IDocumentVectorStore"/> using
/// <see cref="RedisVectorStore"/> from Microsoft.Extensions.VectorData.Redis.
/// </summary>
/// <remarks>
/// Each collection maps to a <see cref="VectorStoreCollection{TKey,TRecord}"/> with Redis
/// hash keys following the pattern <c>rag:{collectionName}:{chunkId}</c>.
/// Document metadata is stored as a separate hash at <c>rag:meta:{collectionName}:{documentId}</c>.
/// </remarks>
public class RedisDocumentVectorStore(
    ILogger<RedisDocumentVectorStore> logger,
    IConnectionMultiplexer connectionMultiplexer
    ) : IDocumentVectorStore
{
    private const string KeyPrefix = "rag";
    private const string MetaPrefix = "rag:meta";

    private VectorStoreCollection<string, DocumentChunk> GetCollection(string collectionName)
    {
        var vectorStore = new RedisVectorStore(connectionMultiplexer.GetDatabase());
        return vectorStore.GetCollection<string, DocumentChunk>($"{KeyPrefix}:{collectionName}");
    }

    /// <inheritdoc/>
    public async Task EnsureCollectionAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection(collectionName);
        await collection.EnsureCollectionExistsAsync(cancellationToken);
        logger.LogInformation("{ClassName} ensured collection {CollectionName}", nameof(RedisDocumentVectorStore), collectionName);
    }

    /// <inheritdoc/>
    public async Task UpsertChunksAsync(string collectionName, IReadOnlyList<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection(collectionName);
        await collection.EnsureCollectionExistsAsync(cancellationToken);

        foreach (var chunk in chunks)
            await collection.UpsertAsync(chunk, cancellationToken);

        logger.LogInformation("{ClassName} upserted {Count} chunks into {CollectionName}",
            nameof(RedisDocumentVectorStore), chunks.Count, collectionName);
    }

    /// <inheritdoc/>
    public async Task<List<DocumentSearchResult>> SearchAsync(string collectionName, ReadOnlyMemory<float> queryEmbedding, int topK, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection(collectionName);
        var results = new List<DocumentSearchResult>();

        await foreach (var result in collection.SearchAsync(queryEmbedding, new VectorSearchOptions { Top = topK }, cancellationToken))
        {
            results.Add(new DocumentSearchResult
            {
                DocumentName = result.Record.DocumentName,
                PageNumber = result.Record.PageNumber,
                Score = result.Score ?? 0,
                Content = result.Record.Content,
            });
        }

        logger.LogDebug("{ClassName} search in {CollectionName} returned {Count} results",
            nameof(RedisDocumentVectorStore), collectionName, results.Count);

        return results;
    }

    /// <inheritdoc/>
    public async Task RemoveDocumentAsync(string collectionName, string documentId, CancellationToken cancellationToken = default)
    {
        var collection = GetCollection(collectionName);
        var db = connectionMultiplexer.GetDatabase();
        var server = connectionMultiplexer.GetServers().First();

        // Find and delete all chunk keys belonging to this document.
        var keyPattern = $"{KeyPrefix}:{collectionName}:{documentId}:*";
        var deletedCount = 0;

        await foreach (var key in server.KeysAsync(pattern: keyPattern))
        {
            await collection.DeleteAsync(key.ToString().Replace($"{KeyPrefix}:{collectionName}:", ""), cancellationToken);
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
        var collection = GetCollection(collectionName);
        await collection.DeleteCollectionAsync(cancellationToken);

        // Also clean up metadata keys.
        var db = connectionMultiplexer.GetDatabase();
        var server = connectionMultiplexer.GetServers().First();

        await foreach (var key in server.KeysAsync(pattern: $"{MetaPrefix}:{collectionName}:*"))
            await db.KeyDeleteAsync(key);

        logger.LogInformation("{ClassName} deleted collection {CollectionName}", nameof(RedisDocumentVectorStore), collectionName);
    }

    /// <inheritdoc/>
    public async Task<List<DocumentInfo>> ListDocumentsAsync(string collectionName, CancellationToken cancellationToken = default)
    {
        var db = connectionMultiplexer.GetDatabase();
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
        var db = connectionMultiplexer.GetDatabase();
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
}
