using Azure.Core;

namespace CasCap.Services;

/// <summary>Azure Table Storage service implementation for Buderus data.</summary>
public class BuderusAzTableStorageService : AzTableStorageBase, IBuderusAzTableStorageService
{
    /// <summary>Initializes a new instance of the <see cref="BuderusAzTableStorageService"/> class.</summary>
    /// <param name="endpoint">Azure Table Storage endpoint URI.</param>
    /// <param name="credential">Token credential for authentication.</param>
    public BuderusAzTableStorageService(Uri endpoint, TokenCredential credential)
        : base(endpoint, credential) { }
}
