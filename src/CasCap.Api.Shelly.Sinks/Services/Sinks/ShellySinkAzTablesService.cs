namespace CasCap.Services;

/// <summary>
/// Event sink that persists <see cref="ShellyEvent"/> instances to Azure Table Storage.
/// Individual events are written to a line-items table and a per-device snapshot row is upserted.
/// </summary>
[SinkType("AzureTables")]
public class ShellySinkAzTablesService : IEventSink<ShellyEvent>, IShellyQuery
{
    private readonly ILogger _logger;
    private readonly TableClient _lineItemTableClient;
    private readonly TableClient _snapshotTableClient;

    private const string SnapshotPartitionKey = "summary";

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellySinkAzTablesService"/> class.
    /// </summary>
    public ShellySinkAzTablesService(ILogger<ShellySinkAzTablesService> logger,
        IOptions<AzureAuthConfig> azureAuthConfig,
        IOptions<ShellyConfig> config)
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
    public async Task WriteEvent(ShellyEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("{ClassName} [{DeviceId}] {@ShellyEvent}", nameof(ShellySinkAzTablesService), @event.DeviceId, @event);

        var lineItemEntity = new ShellyReadingEntity(@event).GetEntity();
        var snapshotEntity = new ShellySnapshotEntity(SnapshotPartitionKey, @event).GetEntity();

        var lineItemTask = _lineItemTableClient.UpsertEntityAsync(lineItemEntity, cancellationToken: cancellationToken);
        var snapshotTask = _snapshotTableClient.UpsertEntityAsync(snapshotEntity, cancellationToken: cancellationToken);

        try
        {
            await Task.WhenAll(lineItemTask, snapshotTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName} {MethodName} failure", nameof(ShellySinkAzTablesService), nameof(WriteEvent));
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<ShellyEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var partitionKey = DateTime.UtcNow.ToString("yyMMdd");
        var entities = _lineItemTableClient.QueryAsync<ShellyReadingEntity>(
            ent => ent.PartitionKey == partitionKey, cancellationToken: cancellationToken);

        var count = 0;
        await foreach (var entity in entities)
        {
            if (++count > Math.Min(limit, 1000))
                yield break;
            yield return new ShellyEvent(entity.DeviceId, string.Empty, entity.Power, entity.RelayState, entity.Temperature, entity.Overpower, entity.TimestampUtc);
        }
    }

    /// <inheritdoc/>
    public async Task<List<ShellySnapshot>> GetSnapshots()
    {
        var snapshots = new List<ShellySnapshot>();
        var entities = _snapshotTableClient.QueryAsync<ShellySnapshotEntity>(
            e => e.PartitionKey == SnapshotPartitionKey);

        await foreach (var entity in entities)
        {
            snapshots.Add(new ShellySnapshot
            {
                DeviceId = entity.RowKey,
                DeviceName = entity.DeviceName,
                Power = entity.Power,
                IsOn = entity.RelayState >= 1,
                Temperature = entity.Temperature,
                Overpower = entity.Overpower >= 1,
                ReadingUtc = entity.ReadingUtc,
            });
        }
        return snapshots;
    }

    /// <summary>
    /// Retrieves smart plug line item readings for the current day.
    /// </summary>
    /// <param name="limit">Maximum number of records to return. Default 100, maximum 1000.</param>
    public async Task<IEnumerable<ShellyReadingEntity>> GetReadings(int limit = 100)
    {
        var partitionKey = DateTime.UtcNow.ToString("yyMMdd");
        _logger.LogInformation("{ClassName} Getting data from table storage for partitionKey '{PartitionKey}'",
            nameof(ShellySinkAzTablesService), partitionKey);
        var entities = await _lineItemTableClient.QueryAsync<ShellyReadingEntity>(
            p => p.PartitionKey == partitionKey
            ).ToListAsync();
        return entities.OrderByDescending(p => p.RowKey).Take(Math.Min(limit, 1000));
    }
}
