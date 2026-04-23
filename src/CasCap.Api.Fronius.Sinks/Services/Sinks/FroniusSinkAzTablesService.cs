namespace CasCap.Services;

/// <summary>
/// Event sink that persists <see cref="FroniusEvent"/> instances to Azure Table Storage.
/// Individual events are written to a line-items table and a single snapshot row is upserted.
/// </summary>
[SinkType("AzureTables")]
public class FroniusSinkAzTablesService : IEventSink<FroniusEvent>, IFroniusQuery
{
    private readonly ILogger _logger;
    private readonly TableClient _lineItemTableClient;
    private readonly TableClient _snapshotTableClient;

    private const string SnapshotPartitionKey = "summary";
    private const string SnapshotRowKey = "latest";

    /// <summary>
    /// Initializes a new instance of the <see cref="FroniusSinkAzTablesService"/> class.
    /// </summary>
    public FroniusSinkAzTablesService(ILogger<FroniusSinkAzTablesService> logger,
        IOptions<AzureAuthConfig> azureAuthConfig,
        IOptions<FroniusConfig> config)
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
    public async Task WriteEvent(FroniusEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("{ClassName} {@FroniusEvent}", nameof(FroniusSinkAzTablesService), @event);

        var lineItemEntity = new FroniusReadingEntity(@event).GetEntity();
        var snapshotEntity = new FroniusSnapshotEntity(SnapshotPartitionKey, SnapshotRowKey, @event).GetEntity();

        var lineItemTask = _lineItemTableClient.UpsertEntityAsync(lineItemEntity, cancellationToken: cancellationToken);
        var snapshotTask = _snapshotTableClient.UpsertEntityAsync(snapshotEntity, cancellationToken: cancellationToken);

        try
        {
            await Task.WhenAll(lineItemTask, snapshotTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName} {MethodName} failure", nameof(FroniusSinkAzTablesService), nameof(WriteEvent));
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<FroniusEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var partitionKey = DateTime.UtcNow.ToString("yyMMdd");
        var entities = _lineItemTableClient.QueryAsync<FroniusReadingEntity>(
            ent => ent.PartitionKey == partitionKey, cancellationToken: cancellationToken);

        var count = 0;
        await foreach (var entity in entities)
        {
            if (++count > Math.Min(limit, 1000))
                yield break;
            yield return new FroniusEvent(entity.SOC, entity.P_Akku, entity.P_Grid, entity.P_Load, entity.P_PV, entity.TimestampUtc);
        }
    }

    /// <inheritdoc/>
    public async Task<InverterSnapshot> GetSnapshot()
    {
        var entity = await GetSnapshotEntity();
        return new InverterSnapshot
        {
            StateOfCharge = entity.SOC,
            BatteryPower = entity.P_Akku,
            GridPower = entity.P_Grid,
            LoadPower = entity.P_Load,
            PhotovoltaicPower = entity.P_PV,
            ReadingUtc = entity.ReadingUtc,
        };
    }

    /// <summary>
    /// Retrieves the latest solar snapshot entity from Azure Table Storage.
    /// </summary>
    internal async Task<FroniusSnapshotEntity> GetSnapshotEntity()
    {
        var response = await _snapshotTableClient.GetEntityIfExistsAsync<FroniusSnapshotEntity>(SnapshotPartitionKey, SnapshotRowKey);
        return response.HasValue ? response.Value! : new FroniusSnapshotEntity();
    }

    /// <summary>
    /// Retrieves solar line item readings for the current day.
    /// </summary>
    /// <param name="limit">Maximum number of records to return. Default 100, maximum 1000.</param>
    public async Task<IEnumerable<FroniusReadingEntity>> GetReadings(int limit = 100)
    {
        var partitionKey = DateTime.UtcNow.ToString("yyMMdd");
        _logger.LogInformation("{ClassName} Getting data from table storage for partitionKey '{PartitionKey}'",
            nameof(FroniusSinkAzTablesService), partitionKey);
        var entities = await _lineItemTableClient.QueryAsync<FroniusReadingEntity>(
            p => p.PartitionKey == partitionKey
            ).ToListAsync();
        return entities.OrderByDescending(p => p.RowKey).Take(Math.Min(limit, 1000));
    }
}
