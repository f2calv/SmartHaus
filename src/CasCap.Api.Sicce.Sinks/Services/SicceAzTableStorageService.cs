using Azure.Core;

namespace CasCap.Services;

/// <summary>Azure Table Storage service implementation for Sicce water pump data.</summary>
public class SicceAzTableStorageService : AzTableStorageBase, ISicceAzTableStorageService
{
    /// <summary>Initializes a new instance of the <see cref="SicceAzTableStorageService"/> class.</summary>
    /// <param name="endpoint">Azure Table Storage endpoint URI.</param>
    /// <param name="credential">Token credential for authentication.</param>
    public SicceAzTableStorageService(Uri endpoint, TokenCredential credential)
        : base(endpoint, credential) { }
}
