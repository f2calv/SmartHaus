namespace CasCap.Services;

/// <summary>
/// Provides access to edge hardware data by querying the primary sink.
/// </summary>
/// <remarks>
/// Key functions are also made accessible via <see cref="Controllers.EdgeHardwareController"/>.
/// Queries are delegated to the keyed <see cref="SinkServiceCollectionExtensions.PrimarySinkKey"/> sink.
/// </remarks>
public class EdgeHardwareQueryService(ILogger<EdgeHardwareQueryService> logger,
    IEdgeHardwareQuery edgeHardwareQuery) : IEdgeHardwareQueryService
{
    /// <inheritdoc/>
    public async Task<List<EdgeHardwareSnapshot>> GetLatestSnapshots()
    {
        logger.LogDebug("{ClassName} retrieving latest snapshots", nameof(EdgeHardwareQueryService));
        return await edgeHardwareQuery.GetSnapshots();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<EdgeHardwareEvent> GetEvents(int limit = 100,
        CancellationToken cancellationToken = default)
        => edgeHardwareQuery.GetEvents(limit: limit, cancellationToken: cancellationToken);
}
