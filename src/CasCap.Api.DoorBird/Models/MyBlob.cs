namespace CasCap.Models;

/// <summary>
/// Represents a blob with its raw content and associated metadata.
/// </summary>
public record MyBlob : IMyBlob
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MyBlob"/> record with an explicit creation date.
    /// </summary>
    [SetsRequiredMembers]
    public MyBlob(byte[] bytes, string blobName, DateTime dt)
    {
        this.bytes = bytes;
        BlobName = blobName;
        this.DateCreatedUtc = dt;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MyBlob"/> record using <see cref="DateTime.UtcNow"/> as the creation date.
    /// </summary>
    [SetsRequiredMembers]
    public MyBlob(byte[] bytes, string blobName)
    {
        this.bytes = bytes;
        BlobName = blobName;
        this.DateCreatedUtc = DateTime.UtcNow;
    }

    /// <inheritdoc/>
    [Description("Raw image bytes.")]
    public required byte[] bytes { get; init; }

    /// <inheritdoc/>
    [Description("UTC timestamp when the blob was created.")]
    public required DateTime DateCreatedUtc { get; init; }

    /// <inheritdoc/>
    [Description("Name identifying this blob (e.g. the method that produced it).")]
    public required string BlobName { get; init; }

    /// <inheritdoc/>
    [Description("Size of the blob in bytes.")]
    public int SizeInBytes => bytes.Length;

    /// <inheritdoc/>
    [Description("True when the blob contains image data; false when empty.")]
    public bool HasImage => bytes.Length > 0;
}
