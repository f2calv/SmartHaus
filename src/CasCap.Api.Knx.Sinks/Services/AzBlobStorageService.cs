using Azure.Core;

namespace CasCap.Services;

/// <summary>KNX-specific Azure Blob Storage service.</summary>
public class KnxAzBlobStorageService : AzBlobStorageBase, IKnxAzBlobStorageService
{
    /// <summary>Initializes a new instance using a connection string.</summary>
    public KnxAzBlobStorageService(string connectionString, string containerName)
        : base(connectionString, containerName) { }

    /// <summary>Initializes a new instance using a URI and token credential.</summary>
    public KnxAzBlobStorageService(Uri blobContainerUri, string containerName, TokenCredential credential)
        : base(blobContainerUri, containerName, credential) { }
}
