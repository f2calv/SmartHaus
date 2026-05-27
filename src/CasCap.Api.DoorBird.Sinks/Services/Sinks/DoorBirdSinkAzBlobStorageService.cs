namespace CasCap.Services;

/// <summary>
/// Event sink that queues <see cref="DoorBirdEvent"/> images for upload to Azure Blob Storage.
/// </summary>
[SinkType("AzBlob")]
public partial class DoorBirdSinkAzBlobStorageService(ILogger<DoorBirdSinkAzBlobStorageService> logger) : IEventSink<DoorBirdEvent>
{
    /// <inheritdoc/>
    public string SinkType => "AzBlob";


    /// <inheritdoc/>
    public IAsyncEnumerable<DoorBirdEvent> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public async Task WriteEvent(DoorBirdEvent @event, CancellationToken cancellationToken = default)
    {
        LogWriteEvent(logger, nameof(DoorBirdSinkAzBlobStorageService), @event.DoorBirdEventType.ToString());
        if (@event.bytes is not null)
        {
            var blob = new MyBlob(@event.bytes, @event.FileName ?? string.Empty, @event.DateCreatedUtc);
            await BlobStatics.UploadQueue.Writer.WriteAsync(blob);
        }
    }

    [LoggerMessage(Level = LogLevel.Trace, Message = "{ClassName} processing {EventType} blob upload")]
    private static partial void LogWriteEvent(ILogger logger, string className, string eventType);
}
