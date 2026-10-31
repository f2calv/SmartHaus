namespace CasCap.Services;

/// <summary>Background service for processing DoorBird image blobs.</summary>
/// <param name="logger">Logger instance.</param>
/// <param name="doorBirdAzBlobStorageSvc">Azure Blob Storage service for DoorBird.</param>
public sealed partial class BlobProcessorBgService(
    ILogger<BlobProcessorBgService> logger,
    IDoorBirdAzBlobStorageService doorBirdAzBlobStorageSvc) : IBgFeature
{
    /// <inheritdoc/>
    public string FeatureName => IBgFeature.AlwaysEnabled;

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} starting", nameof(BlobProcessorBgService));
        await RunServiceAsync(cancellationToken);
        logger.LogInformation("{ClassName} exiting", nameof(BlobProcessorBgService));
    }

    private async Task RunServiceAsync(CancellationToken cancellationToken)
    {
        await foreach (var blob in BlobStatics.UploadQueue.Reader.ReadAllAsync(cancellationToken))
        {
            LogProcessingBlob(logger, nameof(BlobProcessorBgService), blob.BlobName);
            await doorBirdAzBlobStorageSvc.UploadBlob(blob.BlobName, blob.Bytes, cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ClassName} now processing {BlobName}")]
    private static partial void LogProcessingBlob(ILogger logger, string className, string blobName);
}
