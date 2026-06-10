namespace CasCap.Services;

/// <summary>
/// Provides access to Sicce water pump data by querying the primary sink or the device directly.
/// </summary>
/// <remarks>
/// Key functions are also made accessible via <see cref="Controllers.SicceController"/>.
/// Queries are delegated to the keyed <see cref="SinkServiceCollectionExtensions.PrimarySinkKey"/> sink.
/// </remarks>
public sealed class SicceQueryService(
    ILogger<SicceQueryService> logger,
    SicceClientService clientSvc,
    ISicceQuery sicceQuery) : ISicceQueryService
{
    /// <inheritdoc/>
    public async Task<DeviceInfo?> GetDeviceInfo()
    {
        logger.LogDebug("{ClassName} retrieving device info", nameof(SicceQueryService));
        return await clientSvc.GetDeviceInfo().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<SicceSnapshot> GetSnapshot()
    {
        return await sicceQuery.GetSnapshot().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<SicceEvent> GetReadings(
        int limit = 100,
        CancellationToken cancellationToken = default)
        => sicceQuery.GetEvents(limit: limit, cancellationToken: cancellationToken);
}
