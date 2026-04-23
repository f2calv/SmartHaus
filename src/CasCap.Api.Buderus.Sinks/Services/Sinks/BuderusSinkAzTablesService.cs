namespace CasCap.Services;

/// <summary>
/// Event sink that persists <see cref="BuderusEvent"/> instances to Azure Table Storage.
/// Individual events are written to a line-items table and a single running snapshot row
/// is maintained via merge-upsert — each datapoint becomes a column.
/// </summary>
[SinkType("AzureTables")]
public class BuderusSinkAzTablesService : IEventSink<BuderusEvent>, IBuderusQuery
{
    private readonly ILogger _logger;
    private readonly TableClient _lineItemTableClient;
    private readonly TableClient _snapshotTableClient;
    private readonly Dictionary<string, DatapointMapping> _datapointMappings;

    private const string SnapshotPartitionKey = "summary";
    private const string SnapshotRowKey = "latest";

    /// <summary>
    /// Initializes a new instance of the <see cref="BuderusSinkAzTablesService"/> class.
    /// </summary>
    public BuderusSinkAzTablesService(ILogger<BuderusSinkAzTablesService> logger, IOptions<BuderusConfig> config,
        IOptions<AzureAuthConfig> azureAuthConfig)
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
        _datapointMappings = config.Value.DatapointMappings;
    }

    /// <inheritdoc/>
    public async Task WriteEvent(BuderusEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("{ClassName} {@BuderusEvent}", nameof(BuderusSinkAzTablesService), @event);

        var lineItemEntity = new BuderusReadingEntity(@event).GetEntity();
        var lineItemTask = _lineItemTableClient.UpsertEntityAsync(lineItemEntity, cancellationToken: cancellationToken);

        Task? snapshotTask = null;
        if (_datapointMappings.TryGetValue(@event.Id, out var mapping))
        {
            var snapshotUpdate = new TableEntity(SnapshotPartitionKey, SnapshotRowKey)
            {
                { mapping.ColumnName, @event.Value },
            };
            snapshotTask = _snapshotTableClient.UpsertEntityAsync(snapshotUpdate, TableUpdateMode.Merge, cancellationToken);
        }

        try
        {
            if (snapshotTask is not null)
                await Task.WhenAll(lineItemTask, snapshotTask);
            else
                await lineItemTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName} {MethodName} failure", nameof(BuderusSinkAzTablesService), nameof(WriteEvent));
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<BuderusEvent> GetEvents(string? id = null, int limit = 1000, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var partitionKey = DateTime.UtcNow.ToString("yyMMdd");
        AsyncPageable<BuderusReadingEntity> entities;
        if (id is null)
            entities = _lineItemTableClient.QueryAsync<BuderusReadingEntity>(ent => ent.PartitionKey == partitionKey, cancellationToken: cancellationToken);
        else
            entities = _lineItemTableClient.QueryAsync<BuderusReadingEntity>(ent => ent.PartitionKey == partitionKey && ent.d == id, cancellationToken: cancellationToken);

        var count = 0;
        await foreach (var entity in entities)
        {
            if (++count > Math.Min(limit, 1000))
                yield break;
            yield return new BuderusEvent(entity.Id, entity.value, entity.TimestampUtc);
        }
    }

    /// <inheritdoc/>
    public Task<BuderusSnapshot> GetSnapshot()
        => GetSnapshotFromTable();

    /// <summary>
    /// Retrieves the latest Buderus snapshot from Azure Table Storage,
    /// or an empty <see cref="BuderusSnapshot"/> if no snapshot has been written yet.
    /// </summary>
    internal async Task<BuderusSnapshot> GetSnapshotFromTable(CancellationToken cancellationToken = default)
    {
        var response = await _snapshotTableClient.GetEntityIfExistsAsync<TableEntity>(
            SnapshotPartitionKey, SnapshotRowKey, cancellationToken: cancellationToken);
        return BuderusSnapshotExtensions.FromTableEntity(response.HasValue ? response.Value : null);
    }
}
