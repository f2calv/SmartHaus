namespace CasCap.Services;

/// <summary>
/// Provides read-only query access to Dynamic DNS data by delegating to the IP discovery client.
/// </summary>
/// <remarks>
/// Key functions are also made accessible via <see cref="Controllers.DDnsController"/>.
/// </remarks>
public class DDnsQueryService(
    ILogger<DDnsQueryService> logger,
    DDnsFindMyIpClientService findMyIpClientSvc) : IDDnsQueryService
{
    /// <inheritdoc/>
    public Task<IPAddress?> GetCurrentIp(CancellationToken cancellationToken)
    {
        logger.LogDebug("{ClassName} retrieving current external IP", nameof(DDnsQueryService));
        return findMyIpClientSvc.GetIp(cancellationToken);
    }
}
