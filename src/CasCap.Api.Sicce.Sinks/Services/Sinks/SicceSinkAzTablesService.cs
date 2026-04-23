namespace CasCap.Services;

/// <summary>
/// Event sink that persists <see cref="SicceEvent"/> instances to Azure Table Storage.
/// Individual events are written to a line-items table and a single snapshot row is upserted.
/// </summary>
[SinkType("AzureTables")]
public class SicceSinkAzTablesService : IEventSink<SicceEvent>, ISicceQuery
{
    private readonly ILogger _logger;
    private readonly TableClient _lineItemTableClient;
    private readonly TableClient _snapshotTableClient;

    private const string SnapshotPartitionKey = "summary";
    private const string SnapshotRowKey = "latest";

    /// <summary>
    /// Initializes a new instance of the <see cref="SicceSinkAzTablesService"/> class.
    /// </summary>
    public SicceSinkAzTablesService(ILogger<SicceSinkAzTablesService> logger,
        IOptions<AzureAuthConfig> azureAuthConfig,
        IOptions<SicceConfig> config)
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
    public async Task WriteEvent(SicceEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("{ClassName} {@SicceEvent}", nameof(SicceSinkAzTablesService), @event);

        var lineItemEntity = new SicceReadingEntity(@event).GetEntity();
        var snapshotEntity = new SicceSnapshotEntity(SnapshotPartitionKey, SnapshotRowKey, @event).GetEntity();

        var lineItemTask = _lineItemTableClient.UpsertEntityAsync(lineItemEntity, cancellationToken: cancellationToken);
        var snapshotTask = _snapshotTableClient.UpsertEntityAsync(snapshotEntity, cancellationToken: cancellationToken);

        try
        {
            await Task.WhenAll(lineItemTask, snapshotTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName} {MethodName} failure", nameof(SicceSinkAzTablesService), nameof(WriteEvent));
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<SicceEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var partitionKey = DateTime.UtcNow.ToString("yyMMdd");
        var entities = _lineItemTableClient.QueryAsync<SicceReadingEntity>(
            ent => ent.PartitionKey == partitionKey, cancellationToken: cancellationToken);

        var count = 0;
        await foreach (var entity in entities)
        {
            if (++count > Math.Min(limit, 1000))
                yield break;
            yield return new SicceEvent(entity.Temperature, entity.Power, entity.IsOnline, entity.PowerSwitch, entity.TimestampUtc);
        }
    }

    /// <inheritdoc/>
    public async Task<SicceSnapshot> GetSnapshot()
    {
        var entity = await GetSnapshotEntity();
        return new SicceSnapshot
        {
            Temperature = entity.Temperature,
            Power = entity.Power,
            IsOnline = entity.IsOnline,
            PowerSwitch = entity.PowerSwitch,
            ReadingUtc = entity.ReadingUtc,
        };
    }

    /// <summary>
    /// Retrieves the latest Sicce snapshot entity from Azure Table Storage.
    /// </summary>
    internal async Task<SicceSnapshotEntity> GetSnapshotEntity()
    {
        var response = await _snapshotTableClient.GetEntityIfExistsAsync<SicceSnapshotEntity>(SnapshotPartitionKey, SnapshotRowKey);
        return response.HasValue ? response.Value! : new SicceSnapshotEntity();
    }

    /// <summary>
    /// Retrieves Sicce line item readings for the current day.
    /// </summary>
    /// <param name="limit">Maximum number of records to return. Default 100, maximum 1000.</param>
    public async Task<IEnumerable<SicceReadingEntity>> GetReadings(int limit = 100)
    {
        var partitionKey = DateTime.UtcNow.ToString("yyMMdd");
        _logger.LogInformation("{ClassName} Getting data from table storage for partitionKey {PartitionKey}",
            nameof(SicceSinkAzTablesService), partitionKey);
        var entities = await _lineItemTableClient.QueryAsync<SicceReadingEntity>(
            p => p.PartitionKey == partitionKey
            ).ToListAsync();
        return entities.OrderByDescending(p => p.RowKey).Take(Math.Min(limit, 1000));
    }
}
