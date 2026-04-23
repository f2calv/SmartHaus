namespace CasCap.Services;

/// <summary>
/// Event sink that persists <see cref="DoorBirdEvent"/> instances to Azure Table Storage.
/// Individual events are written to a line-items table and a running summary is maintained
/// in a snapshot table. Counts are tracked in-memory (seeded from Table Storage on first use)
/// and only the columns affected by each event are updated via merge-upsert.
/// </summary>
[SinkType("AzureTables")]
public class DoorBirdSinkAzTablesService : IEventSink<DoorBirdEvent>, IDoorBirdQuery
{
    private readonly ILogger _logger;
    private readonly TableClient _lineItemTableClient;
    private readonly TableClient _snapshotTableClient;

    private const string SnapshotPartitionKey = "summary";
    private const string SnapshotRowKey = "latest";

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _countersInitialized;
    private int _doorbellCount;
    private int _motionCount;
    private int _rfidCount;
    private int _relayTriggerCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="DoorBirdSinkAzTablesService"/> class.
    /// </summary>
    public DoorBirdSinkAzTablesService(ILogger<DoorBirdSinkAzTablesService> logger,
        IOptions<AzureAuthConfig> azureAuthConfig,
        IOptions<DoorBirdConfig> config)
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
    public async Task WriteEvent(DoorBirdEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("{ClassName} {@DoorBirdEvent}", nameof(DoorBirdSinkAzTablesService), @event);

        await EnsureCountersInitializedAsync(cancellationToken);

        var lineItemEntity = new DoorBirdReadingEntity(@event).GetEntity();
        var snapshotUpdate = BuildSnapshotUpdate(@event);

        var tasks = new List<Task>(2)
        {
            _lineItemTableClient.UpsertEntityAsync(lineItemEntity, cancellationToken: cancellationToken),
            _snapshotTableClient.UpsertEntityAsync(snapshotUpdate, TableUpdateMode.Merge, cancellationToken),
        };

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName} {MethodName} failure", nameof(DoorBirdSinkAzTablesService), nameof(WriteEvent));
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<DoorBirdEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var partitionKey = DateTime.UtcNow.ToString("yyMMdd");
        AsyncPageable<DoorBirdReadingEntity> entities;
        if (id is null)
            entities = _lineItemTableClient.QueryAsync<DoorBirdReadingEntity>(ent => ent.PartitionKey == partitionKey, cancellationToken: cancellationToken);
        else
            entities = _lineItemTableClient.QueryAsync<DoorBirdReadingEntity>(ent => ent.PartitionKey == partitionKey && ent.t == id, cancellationToken: cancellationToken);

        var count = 0;
        await foreach (var entity in entities)
        {
            if (++count > Math.Min(limit, 1000))
                yield break;
            yield return new DoorBirdEvent
            {
                DoorBirdEventType = Enum.Parse<DoorBirdEventType>(entity.EventType),
                DateCreatedUtc = entity.TimestampUtc,
            };
        }
    }

    /// <inheritdoc/>
    public async Task<DoorBirdSnapshot> GetSnapshot()
    {
        var entity = await GetSnapshotEntity();
        return new DoorBirdSnapshot
        {
            SnapshotUtc = DateTime.UtcNow,
            LastDoorbellUtc = entity.LastDoorbellUtc?.UtcDateTime,
            LastMotionUtc = entity.LastMotionUtc?.UtcDateTime,
            LastRfidUtc = entity.LastRfidUtc?.UtcDateTime,
            LastRelayTriggerUtc = entity.LastRelayTriggerUtc?.UtcDateTime,
            DoorbellCount = entity.DoorbellCount,
            MotionCount = entity.MotionCount,
            RfidCount = entity.RfidCount,
            RelayTriggerCount = entity.RelayTriggerCount,
        };
    }

    /// <summary>
    /// Retrieves the latest DoorBird activity snapshot entity from Azure Table Storage,
    /// or a default empty entity if no snapshot has been written yet.
    /// </summary>
    internal async Task<DoorBirdSnapshotEntity> GetSnapshotEntity(CancellationToken cancellationToken = default)
    {
        var response = await _snapshotTableClient.GetEntityIfExistsAsync<DoorBirdSnapshotEntity>(
            SnapshotPartitionKey, SnapshotRowKey, cancellationToken: cancellationToken);
        return response.HasValue ? response.Value! : new DoorBirdSnapshotEntity();
    }

    /// <summary>
    /// Retrieves DoorBird event line items for the current day.
    /// </summary>
    /// <param name="limit">Maximum number of records to return. Default 100, maximum 1000.</param>
    public async Task<IEnumerable<DoorBirdReadingEntity>> GetReadings(int limit = 100)
    {
        var partitionKey = DateTime.UtcNow.ToString("yyMMdd");
        _logger.LogInformation("{ClassName} Getting data from table storage for partitionKey {PartitionKey}",
            nameof(DoorBirdSinkAzTablesService), partitionKey);
        var entities = await _lineItemTableClient.QueryAsync<DoorBirdReadingEntity>(
            p => p.PartitionKey == partitionKey
            ).ToListAsync();
        return entities.OrderByDescending(p => p.RowKey).Take(Math.Min(limit, 1000));
    }

    #region private helpers

    /// <summary>
    /// Seeds in-memory counters from Table Storage on first use. Uses a <see cref="SemaphoreSlim"/>
    /// to ensure only one caller reads; subsequent callers skip the check.
    /// </summary>
    private async Task EnsureCountersInitializedAsync(CancellationToken cancellationToken)
    {
        if (_countersInitialized)
            return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_countersInitialized)
                return;

            var entity = await GetSnapshotEntity(cancellationToken);
            _doorbellCount = entity.DoorbellCount;
            _motionCount = entity.MotionCount;
            _rfidCount = entity.RfidCount;
            _relayTriggerCount = entity.RelayTriggerCount;
            _countersInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Increments the relevant in-memory counter and builds a partial
    /// <see cref="TableEntity"/> containing only the two columns affected by this event.
    /// The caller merge-upserts this entity so all other columns are preserved.
    /// </summary>
    private TableEntity BuildSnapshotUpdate(DoorBirdEvent doorBirdEvent)
    {
        var entity = new TableEntity(SnapshotPartitionKey, SnapshotRowKey);
        var utc = new DateTimeOffset(doorBirdEvent.DateCreatedUtc, TimeSpan.Zero);

        switch (doorBirdEvent.DoorBirdEventType)
        {
            case DoorBirdEventType.Doorbell:
                entity[nameof(DoorBirdSnapshotEntity.LastDoorbellUtc)] = utc;
                entity[nameof(DoorBirdSnapshotEntity.DoorbellCount)] = Interlocked.Increment(ref _doorbellCount);
                break;
            case DoorBirdEventType.MotionSensor:
                entity[nameof(DoorBirdSnapshotEntity.LastMotionUtc)] = utc;
                entity[nameof(DoorBirdSnapshotEntity.MotionCount)] = Interlocked.Increment(ref _motionCount);
                break;
            case DoorBirdEventType.Rfid:
                entity[nameof(DoorBirdSnapshotEntity.LastRfidUtc)] = utc;
                entity[nameof(DoorBirdSnapshotEntity.RfidCount)] = Interlocked.Increment(ref _rfidCount);
                break;
            case DoorBirdEventType.DoorRelay:
                entity[nameof(DoorBirdSnapshotEntity.LastRelayTriggerUtc)] = utc;
                entity[nameof(DoorBirdSnapshotEntity.RelayTriggerCount)] = Interlocked.Increment(ref _relayTriggerCount);
                break;
        }

        return entity;
    }

    #endregion
}
