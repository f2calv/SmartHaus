namespace CasCap.Models;

/// <summary>
/// Describes an image blob without its raw content, so callers can check availability and size cheaply.
/// </summary>
public sealed record MyBlobInfo
{
    /// <summary>Name identifying this blob (e.g. the method that produced it).</summary>
    [Description("Name identifying this blob (e.g. the method that produced it).")]
    public required string BlobName { get; init; }

    /// <summary>UTC timestamp when the blob was created.</summary>
    [Description("UTC timestamp when the blob was created.")]
    public required DateTime DateCreatedUtc { get; init; }

    /// <summary>Size of the blob in bytes.</summary>
    [Description("Size of the blob in bytes.")]
    public required int SizeInBytes { get; init; }

    /// <summary>True when the blob contains image data; false when empty.</summary>
    [Description("True when the blob contains image data; false when empty.")]
    public bool HasImage => SizeInBytes > 0;
}
