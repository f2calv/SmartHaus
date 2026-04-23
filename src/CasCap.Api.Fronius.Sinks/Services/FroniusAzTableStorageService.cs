using Azure.Core;

namespace CasCap.Services;

/// <summary>Azure Table Storage service implementation for Fronius Symo inverter data.</summary>
public class FroniusAzTableStorageService : AzTableStorageBase, IFroniusSymoAzTableStorageService
{
    /// <summary>Initializes a new instance of the <see cref="FroniusAzTableStorageService"/> class.</summary>
    /// <param name="endpoint">Azure Table Storage endpoint URI.</param>
    /// <param name="credential">Token credential for authentication.</param>
    public FroniusAzTableStorageService(Uri endpoint, TokenCredential credential)
        : base(endpoint, credential) { }
}
