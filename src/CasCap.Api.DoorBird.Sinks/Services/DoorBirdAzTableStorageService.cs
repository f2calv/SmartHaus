using Azure.Core;

namespace CasCap.Services;

/// <summary>Azure Table Storage service implementation for DoorBird data.</summary>
public class DoorBirdAzTableStorageService : AzTableStorageBase, IDoorBirdAzTableStorageService
{
    /// <summary>Initializes a new instance of the <see cref="DoorBirdAzTableStorageService"/> class.</summary>
    /// <param name="endpoint">Azure Table Storage endpoint URI.</param>
    /// <param name="credential">Token credential for authentication.</param>
    public DoorBirdAzTableStorageService(Uri endpoint, TokenCredential credential)
        : base(endpoint, credential) { }
}
