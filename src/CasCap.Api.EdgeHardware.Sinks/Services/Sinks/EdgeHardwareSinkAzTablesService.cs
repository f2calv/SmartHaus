namespace CasCap.Services;

/// <summary>
/// Event sink that persists <see cref="EdgeHardwareEvent"/> instances to Azure Table Storage.
/// Individual events are written to a line-items table and a single snapshot row is upserted.
/// </summary>
[SinkType("AzureTables")]
public class EdgeHardwareSinkAzTablesService : IEventSink<EdgeHardwareEvent>, IEdgeHardwareQuery
{
    private readonly ILogger _logger;
    private readonly TableClient _lineItemTableClient;
    private readonly TableClient _snapshotTableClient;

    private const string SnapshotPartitionKey = "summary";

    /// <summary>
    /// Initializes a new instance of the <see cref="EdgeHardwareSinkAzTablesService"/> class.
    /// </summary>
    public EdgeHardwareSinkAzTablesService(ILogger<EdgeHardwareSinkAzTablesService> logger,
        IOptions<AzureAuthConfig> azureAuthConfig,
        IOptions<EdgeHardwareConfig> config)
    {
        _logger = logger;

        var azConfig = config.Value.Sinks.AvailableSinks["AzureTables"];
        var connectionString = config.Value.AzureTableStorageConnectionString;
        var lineItemTableName = azConfig.GetSetting(SinkSettingKeys.LineItemTableName)!;
        var snapshotTableName = azConfig.GetSetting(SinkSettingKeys.SnapshotTableName)!;
        _lineItemTableClient = StorageExtensions.CreateTableClient(connectionString, lineItemTableName, azureAuthConfig.Value.TokenCredential);
        _snapshotTableClient = StorageExtensions.CreateTableClient(connectionString, snapshotTableName, azureAuthConfig.Value.TokenCredential);
        _lineItemTableClient.CreateIfNotExists();
        _snapshotTableClient.CreateIfNotExists();
    }

    /// <inheritdoc/>
    public async Task WriteEvent(EdgeHardwareEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("{ClassName} {@Data}", nameof(EdgeHardwareSinkAzTablesService), @event);

        var lineItemEntity = new EdgeHardwareReadingEntity(@event).GetEntity();
        var snapshotEntity = new EdgeHardwareSnapshotEntity(SnapshotPartitionKey, @event).GetEntity();

        var lineItemTask = _lineItemTableClient.UpsertEntityAsync(lineItemEntity, cancellationToken: cancellationToken);
        var snapshotTask = _snapshotTableClient.UpsertEntityAsync(snapshotEntity, cancellationToken: cancellationToken);

        try
        {
            await Task.WhenAll(lineItemTask, snapshotTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName} {MethodName} failure", nameof(EdgeHardwareSinkAzTablesService), nameof(WriteEvent));
        }
    }

    /// <inheritdoc/>
    public async Task<List<EdgeHardwareSnapshot>> GetSnapshots()
    {
        var snapshots = new List<EdgeHardwareSnapshot>();
        var entities = _snapshotTableClient.QueryAsync<EdgeHardwareSnapshotEntity>(
            e => e.PartitionKey == SnapshotPartitionKey);

        await foreach (var entity in entities)
        {
            snapshots.Add(new EdgeHardwareSnapshot
            {
                NodeName = entity.NodeName,
                GpuPowerDrawW = entity.GpuPowerDrawW,
                GpuTemperatureC = entity.GpuTemperatureC,
                GpuUtilizationPercent = entity.GpuUtilizationPercent,
                GpuMemoryUtilizationPercent = entity.GpuMemoryUtilizationPercent,
                GpuMemoryUsedMiB = entity.GpuMemoryUsedMiB,
                GpuMemoryTotalMiB = entity.GpuMemoryTotalMiB,
                CpuTemperatureC = entity.CpuTemperatureC,
                Timestamp = entity.ReadingUtc ?? DateTimeOffset.MinValue,
            });
        }
        return snapshots;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<EdgeHardwareEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var partitionKey = DateTime.UtcNow.ToString("yyMMdd");
        _logger.LogInformation("{ClassName} getting data from table storage for partitionKey {PartitionKey}",
            nameof(EdgeHardwareSinkAzTablesService), partitionKey);

        var entities = await _lineItemTableClient.QueryAsync<EdgeHardwareReadingEntity>(
            p => p.PartitionKey == partitionKey, cancellationToken: cancellationToken).ToListAsync(cancellationToken);

        foreach (var entity in entities.OrderByDescending(p => p.RowKey).Take(Math.Min(limit, 1000)))
        {
            yield return new EdgeHardwareEvent
            {
                NodeName = entity.n ?? "unknown",
                TimestampUtc = entity.TimestampUtc,
                GpuPowerDrawW = entity.pw,
                GpuTemperatureC = entity.gt,
                GpuUtilizationPercent = entity.gu,
                GpuMemoryUtilizationPercent = entity.mu,
                GpuMemoryUsedMiB = entity.mm,
                GpuMemoryTotalMiB = entity.mt,
                CpuTemperatureC = entity.ct,
            };
        }
    }
}
