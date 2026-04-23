namespace CasCap.Abstractions;

/// <summary>Minimal blob storage contract used by edge hardware services.</summary>
public interface IBlobStorage
{
    /// <summary>Uploads a byte array as a blob with the specified name.</summary>
    Task UploadBlob(string blobName, byte[] bytes, CancellationToken cancellationToken);
}
