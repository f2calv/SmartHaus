namespace CasCap.Services;

/// <summary>
/// Event sink that queues <see cref="DoorBirdEvent"/> images for upload to Azure Blob Storage.
/// </summary>
[SinkType("AzBlob")]
public partial class DoorBirdSinkAzBlobService(ILogger<DoorBirdSinkAzBlobService> logger) : IEventSink<DoorBirdEvent>
{
    /// <inheritdoc/>
    public string SinkType => "AzBlob";



    /// <inheritdoc/>
    public async Task WriteEvent(DoorBirdEvent @event, CancellationToken cancellationToken = default)
    {
        LogWriteEvent(logger, nameof(DoorBirdSinkAzBlobService), @event.DoorBirdEventType.ToString());
        if (@event.bytes is not null)
        {
            var blob = new MyBlob(@event.bytes, @event.FileName ?? string.Empty, @event.DateCreatedUtc);
            await BlobStatics.UploadQueue.Writer.WriteAsync(blob);
        }
    }

    [LoggerMessage(Level = LogLevel.Trace, Message = "{ClassName} processing {EventType} blob upload")]
    private static partial void LogWriteEvent(ILogger logger, string className, string eventType);
}
