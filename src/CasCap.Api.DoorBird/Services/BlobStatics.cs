namespace CasCap.Services;

/// <summary>Static storage for DoorBird blob upload queue.</summary>
public class BlobStatics
{
    /// <summary>Channel for queuing blob uploads.</summary>
    public static Channel<IMyBlob> UploadQueue { get; set; }
        = Channel.CreateBounded<IMyBlob>(new BoundedChannelOptions(1_000) { SingleReader = true });
}
