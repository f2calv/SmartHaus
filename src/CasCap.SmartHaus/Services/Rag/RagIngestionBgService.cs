namespace CasCap.Services;

/// <summary>
/// Background service that automatically ingests PDF documents from the configured
/// <see cref="RagConfig.DocumentsPath"/> directory into the Redis vector store on startup.
/// </summary>
public class RagIngestionBgService(
    ILogger<RagIngestionBgService> logger,
    IDocumentIngestionService ingestionSvc,
    IDocumentVectorStore vectorStore,
    IOptions<RagConfig> ragConfig
    ) : IBgFeature
{
    /// <inheritdoc/>
    public string FeatureName => FeatureNames.Rag;

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken) =>
        ExecuteAsync(cancellationToken);

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var config = ragConfig.Value;

        if (!config.AutoIngestOnStartup)
        {
            logger.LogInformation("{ClassName} auto-ingestion disabled", nameof(RagIngestionBgService));
            return;
        }

        if (!Directory.Exists(config.DocumentsPath))
        {
            logger.LogWarning("{ClassName} documents directory {Path} does not exist, skipping auto-ingestion",
                nameof(RagIngestionBgService), config.DocumentsPath);
            return;
        }

        try
        {
            logger.LogInformation("{ClassName} starting auto-ingestion from {Path} into {Collection}",
                nameof(RagIngestionBgService), config.DocumentsPath, config.IndexName);

            await vectorStore.EnsureCollectionAsync(config.IndexName, cancellationToken);

            var results = await ingestionSvc.IngestDirectoryAsync(config.DocumentsPath, config.IndexName, cancellationToken);

            logger.LogInformation("{ClassName} auto-ingestion complete — {Count} documents ingested",
                nameof(RagIngestionBgService), results.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "{ClassName} auto-ingestion failed", nameof(RagIngestionBgService));
        }
    }
}
