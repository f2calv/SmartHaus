using Azure.Core;

namespace CasCap.Services;

/// <summary>Azure Table Storage service implementation for Ubiquiti data.</summary>
public class UbiquitiAzTableStorageService : AzTableStorageBase, IUbiquitiAzTableStorageService
{
    /// <summary>Initializes a new instance of the <see cref="UbiquitiAzTableStorageService"/> class.</summary>
    /// <param name="endpoint">Azure Table Storage endpoint URI.</param>
    /// <param name="credential">Token credential for authentication.</param>
    public UbiquitiAzTableStorageService(Uri endpoint, TokenCredential credential)
        : base(endpoint, credential) { }
}
