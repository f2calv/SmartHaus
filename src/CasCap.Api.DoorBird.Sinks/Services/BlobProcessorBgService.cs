namespace CasCap.Services;

/// <summary>Background service for processing DoorBird image blobs.</summary>
/// <param name="logger">Logger instance.</param>
/// <param name="doorBirdAzBlobStorageSvc">Azure Blob Storage service for DoorBird.</param>
public class BlobProcessorBgService(ILogger<BlobProcessorBgService> logger, IDoorBirdAzBlobStorageService doorBirdAzBlobStorageSvc) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("{ClassName} starting", nameof(BlobProcessorBgService));
        try
        {
            await RunServiceAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException) { throw; }
        logger.LogInformation("{ClassName} exiting", nameof(BlobProcessorBgService));
    }

    private async Task RunServiceAsync(CancellationToken cancellationToken)
    {
        await foreach (var blob in BlobStatics.UploadQueue.Reader.ReadAllAsync(cancellationToken))
        {
            logger.LogDebug("{ClassName} now processing {BlobName}", nameof(BlobProcessorBgService), blob.BlobName);
            await doorBirdAzBlobStorageSvc.UploadBlob(blob.BlobName, blob.bytes, cancellationToken);
        }
    }
}
