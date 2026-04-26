using Microsoft.Extensions.AI;
using UglyToad.PdfPig;

namespace CasCap.Services;

/// <summary>
/// PDF ingestion service that extracts text from PDFs, splits into token-aware chunks,
/// generates embeddings via <see cref="IEmbeddingGenerator{TInput,TEmbedding}"/>,
/// and stores in the vector store.
/// </summary>
public class PdfDocumentIngestionService(
    ILogger<PdfDocumentIngestionService> logger,
    IDocumentVectorStore vectorStore,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    IOptions<RagConfig> ragConfig
    ) : IDocumentIngestionService
{
    /// <inheritdoc/>
    public async Task<DocumentInfo> IngestDocumentAsync(Stream pdfStream, string documentName, string collectionName, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("{ClassName} ingesting {DocumentName} into {CollectionName}",
            nameof(PdfDocumentIngestionService), documentName, collectionName);

        var config = ragConfig.Value;
        var documentId = GenerateDocumentId(documentName);

        // Remove existing chunks for this document before re-ingesting.
        await vectorStore.RemoveDocumentAsync(collectionName, documentId, cancellationToken);

        // Extract text from PDF pages.
        var pages = ExtractPages(pdfStream);
        logger.LogInformation("{ClassName} extracted {PageCount} pages from {DocumentName}",
            nameof(PdfDocumentIngestionService), pages.Count, documentName);

        // Chunk the extracted text.
        var chunks = ChunkPages(pages, documentId, documentName, config.ChunkSizeTokens, config.ChunkOverlapTokens);
        logger.LogInformation("{ClassName} created {ChunkCount} chunks from {DocumentName}",
            nameof(PdfDocumentIngestionService), chunks.Count, documentName);

        if (chunks.Count == 0)
        {
            logger.LogWarning("{ClassName} no text content extracted from {DocumentName}",
                nameof(PdfDocumentIngestionService), documentName);
            return new DocumentInfo
            {
                DocumentId = documentId,
                DocumentName = documentName,
                PageCount = pages.Count,
                ChunkCount = 0,
                IngestedAtUtc = DateTime.UtcNow,
            };
        }

        // Generate embeddings in batches.
        var texts = chunks.Select(c => c.Content).ToList();
        var embeddings = await embeddingGenerator.GenerateAsync(texts, cancellationToken: cancellationToken);

        for (var i = 0; i < chunks.Count; i++)
            chunks[i].Embedding = embeddings[i].Vector;

        // Store in vector store.
        await vectorStore.EnsureCollectionAsync(collectionName, cancellationToken);
        await vectorStore.UpsertChunksAsync(collectionName, chunks, cancellationToken);

        var info = new DocumentInfo
        {
            DocumentId = documentId,
            DocumentName = documentName,
            PageCount = pages.Count,
            ChunkCount = chunks.Count,
            IngestedAtUtc = DateTime.UtcNow,
        };

        // Store metadata.
        if (vectorStore is RedisDocumentVectorStore redisStore)
            await redisStore.StoreDocumentMetadataAsync(collectionName, info);

        logger.LogInformation("{ClassName} ingested {DocumentName} — {PageCount} pages, {ChunkCount} chunks",
            nameof(PdfDocumentIngestionService), documentName, info.PageCount, info.ChunkCount);

        return info;
    }

    /// <inheritdoc/>
    public async Task<List<DocumentInfo>> IngestDirectoryAsync(string directoryPath, string collectionName, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
        {
            logger.LogWarning("{ClassName} directory {Path} does not exist, skipping ingestion",
                nameof(PdfDocumentIngestionService), directoryPath);
            return [];
        }

        var pdfFiles = Directory.GetFiles(directoryPath, "*.pdf", SearchOption.AllDirectories);
        logger.LogInformation("{ClassName} found {Count} PDF files in {Path}",
            nameof(PdfDocumentIngestionService), pdfFiles.Length, directoryPath);

        var results = new List<DocumentInfo>();
        foreach (var pdfFile in pdfFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var stream = File.OpenRead(pdfFile);
                var info = await IngestDocumentAsync(stream, Path.GetFileName(pdfFile), collectionName, cancellationToken);
                results.Add(info);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{ClassName} failed to ingest {File}", nameof(PdfDocumentIngestionService), pdfFile);
            }
        }

        return results;
    }

    /// <inheritdoc/>
    public Task RemoveDocumentAsync(string documentId, string collectionName, CancellationToken cancellationToken = default) =>
        vectorStore.RemoveDocumentAsync(collectionName, documentId, cancellationToken);

    /// <summary>Extracts text from each page of a PDF.</summary>
    private static List<(int PageNumber, string Text)> ExtractPages(Stream pdfStream)
    {
        var pages = new List<(int, string)>();

        using var document = PdfDocument.Open(pdfStream);
        foreach (var page in document.GetPages())
        {
            var text = page.Text;
            if (!string.IsNullOrWhiteSpace(text))
                pages.Add((page.Number, text));
        }

        return pages;
    }

    /// <summary>
    /// Splits extracted pages into chunks using a simple character-based approximation
    /// (4 characters ≈ 1 token). Chunks overlap by <paramref name="overlapTokens"/> tokens.
    /// </summary>
    private static List<DocumentChunk> ChunkPages(
        List<(int PageNumber, string Text)> pages,
        string documentId,
        string documentName,
        int chunkSizeTokens,
        int overlapTokens)
    {
        var chunks = new List<DocumentChunk>();
        var chunkIndex = 0;

        // Approximate token-to-char ratio.
        var chunkSizeChars = chunkSizeTokens * 4;
        var overlapChars = overlapTokens * 4;

        foreach (var (pageNumber, text) in pages)
        {
            var position = 0;
            while (position < text.Length)
            {
                var length = Math.Min(chunkSizeChars, text.Length - position);
                var chunkText = text.Substring(position, length).Trim();

                if (!string.IsNullOrWhiteSpace(chunkText))
                {
                    chunks.Add(new DocumentChunk
                    {
                        Id = $"{documentId}:{chunkIndex}",
                        DocumentId = documentId,
                        DocumentName = documentName,
                        Content = chunkText,
                        PageNumber = pageNumber,
                        ChunkIndex = chunkIndex,
                    });
                    chunkIndex++;
                }

                position += chunkSizeChars - overlapChars;
                if (chunkSizeChars - overlapChars <= 0)
                    break;
            }
        }

        return chunks;
    }

    /// <summary>Generates a stable document ID from the file name.</summary>
    private static string GenerateDocumentId(string documentName) =>
        documentName
            .Replace(' ', '-')
            .Replace('.', '-')
            .ToLowerInvariant();
}
