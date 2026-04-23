namespace CasCap.Services;

/// <summary>
/// Provides access to Sicce water pump data by querying the primary sink or the device directly.
/// </summary>
/// <remarks>
/// Key functions are also made accessible via <see cref="Controllers.SicceController"/>.
/// Queries are delegated to the keyed <see cref="SinkServiceCollectionExtensions.PrimarySinkKey"/> sink.
/// </remarks>
public class SicceQueryService(
    ILogger<SicceQueryService> logger,
    SicceClientService clientSvc,
    [FromKeyedServices(SinkServiceCollectionExtensions.PrimarySinkKey)] IEventSink<SicceEvent> primarySink,
    ISicceQuery? sicceQuery = null) : ISicceQueryService
{
    /// <inheritdoc/>
    public async Task<DeviceInfo?> GetDeviceInfo()
    {
        logger.LogDebug("{ClassName} retrieving device info", nameof(SicceQueryService));
        return await clientSvc.GetDeviceInfo();
    }

    /// <inheritdoc/>
    public async Task<SicceSnapshot> GetSnapshot()
    {
        if (sicceQuery is null)
            return new();
        return await sicceQuery.GetSnapshot();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<SicceEvent> GetReadings(
        int limit = 100,
        CancellationToken cancellationToken = default)
        => primarySink.GetEvents(limit: limit, cancellationToken: cancellationToken);
}
