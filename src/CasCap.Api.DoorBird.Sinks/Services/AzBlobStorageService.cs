using Azure.Core;

namespace CasCap.Services;

/// <summary>DoorBird-specific Azure Blob Storage service.</summary>
public class DoorBirdAzBlobStorageService : AzBlobStorageBase, IDoorBirdAzBlobStorageService
{
    /// <summary>Initializes a new instance using a connection string.</summary>
    public DoorBirdAzBlobStorageService(string connectionString, string containerName)
        : base(connectionString, containerName) { }

    /// <summary>Initializes a new instance using a URI and token credential.</summary>
    public DoorBirdAzBlobStorageService(Uri blobContainerUri, string containerName, TokenCredential credential)
        : base(blobContainerUri, containerName, credential) { }
}
