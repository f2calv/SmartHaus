namespace CasCap.Abstractions;

/// <summary>
/// Defines the public query operations exposed by the Dynamic DNS service.
/// </summary>
public interface IDDnsQueryService
{
    /// <summary>
    /// Retrieves the current external IP address.
    /// </summary>
    public Task<IPAddress?> GetCurrentIp(CancellationToken cancellationToken = default);
}
