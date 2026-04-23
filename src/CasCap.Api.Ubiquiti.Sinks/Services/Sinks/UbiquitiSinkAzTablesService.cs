namespace CasCap.Services;

/// <summary>
/// Event sink that persists <see cref="UbiquitiEvent"/> instances to Azure Table Storage.
/// Individual events are written to a line-items table and a running summary is maintained
/// in a snapshot table. Counts are tracked in-memory (seeded from Table Storage on first use)
/// and only the columns affected by each event are updated via merge-upsert.
/// </summary>
[SinkType("AzureTables")]
public class UbiquitiSinkAzTablesService : IEventSink<UbiquitiEvent>, IUbiquitiQuery
{
    private readonly ILogger _logger;
    private readonly TableClient _lineItemTableClient;
    private readonly TableClient _snapshotTableClient;

    private const string SnapshotPartitionKey = "summary";
    private const string SnapshotRowKey = "latest";

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _countersInitialized;
    private int _motionCount;
    private int _smartDetectPersonCount;
    private int _smartDetectVehicleCount;
    private int _smartDetectAnimalCount;
    private int _smartDetectPackageCount;
    private int _ringCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="UbiquitiSinkAzTablesService"/> class.
    /// </summary>
    public UbiquitiSinkAzTablesService(ILogger<UbiquitiSinkAzTablesService> logger,
        IOptions<AzureAuthConfig> azureAuthConfig,
        IOptions<UbiquitiConfig> config)
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
    public async Task WriteEvent(UbiquitiEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("{ClassName} {@UbiquitiEvent}", nameof(UbiquitiSinkAzTablesService), @event);

        await EnsureCountersInitializedAsync(cancellationToken);

        var lineItemEntity = new UbiquitiReadingEntity(@event).GetEntity();
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
            _logger.LogError(ex, "{ClassName} {MethodName} failure", nameof(UbiquitiSinkAzTablesService), nameof(WriteEvent));
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<UbiquitiEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var partitionKey = DateTime.UtcNow.ToString("yyMMdd");
        AsyncPageable<UbiquitiReadingEntity> entities;
        if (id is null)
            entities = _lineItemTableClient.QueryAsync<UbiquitiReadingEntity>(ent => ent.PartitionKey == partitionKey, cancellationToken: cancellationToken);
        else
            entities = _lineItemTableClient.QueryAsync<UbiquitiReadingEntity>(ent => ent.PartitionKey == partitionKey && ent.t == id, cancellationToken: cancellationToken);

        var count = 0;
        await foreach (var entity in entities)
        {
            if (++count > Math.Min(limit, 1000))
                yield break;
            yield return new UbiquitiEvent
            {
                UbiquitiEventType = Enum.Parse<UbiquitiEventType>(entity.EventType),
                DateCreatedUtc = entity.TimestampUtc,
                CameraId = entity.CameraId,
                CameraName = entity.CameraName,
            };
        }
    }

    /// <inheritdoc/>
    public async Task<UbiquitiSnapshot> GetSnapshot()
    {
        var entity = await GetSnapshotEntity();
        return new UbiquitiSnapshot
        {
            SnapshotUtc = DateTime.UtcNow,
            LastMotionUtc = entity.LastMotionUtc?.UtcDateTime,
            LastSmartDetectPersonUtc = entity.LastSmartDetectPersonUtc?.UtcDateTime,
            LastSmartDetectVehicleUtc = entity.LastSmartDetectVehicleUtc?.UtcDateTime,
            LastSmartDetectAnimalUtc = entity.LastSmartDetectAnimalUtc?.UtcDateTime,
            LastSmartDetectPackageUtc = entity.LastSmartDetectPackageUtc?.UtcDateTime,
            LastRingUtc = entity.LastRingUtc?.UtcDateTime,
            MotionCount = entity.MotionCount,
            SmartDetectPersonCount = entity.SmartDetectPersonCount,
            SmartDetectVehicleCount = entity.SmartDetectVehicleCount,
            SmartDetectAnimalCount = entity.SmartDetectAnimalCount,
            SmartDetectPackageCount = entity.SmartDetectPackageCount,
            RingCount = entity.RingCount,
        };
    }

    /// <summary>
    /// Retrieves the latest Ubiquiti activity snapshot entity from Azure Table Storage,
    /// or a default empty entity if no snapshot has been written yet.
    /// </summary>
    internal async Task<UbiquitiSnapshotEntity> GetSnapshotEntity(CancellationToken cancellationToken = default)
    {
        var response = await _snapshotTableClient.GetEntityIfExistsAsync<UbiquitiSnapshotEntity>(
            SnapshotPartitionKey, SnapshotRowKey, cancellationToken: cancellationToken);
        return response.HasValue ? response.Value! : new UbiquitiSnapshotEntity();
    }

    #region private helpers

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
            _motionCount = entity.MotionCount;
            _smartDetectPersonCount = entity.SmartDetectPersonCount;
            _smartDetectVehicleCount = entity.SmartDetectVehicleCount;
            _smartDetectAnimalCount = entity.SmartDetectAnimalCount;
            _smartDetectPackageCount = entity.SmartDetectPackageCount;
            _ringCount = entity.RingCount;
            _countersInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private TableEntity BuildSnapshotUpdate(UbiquitiEvent ubiquitiEvent)
    {
        var entity = new TableEntity(SnapshotPartitionKey, SnapshotRowKey);
        var utc = new DateTimeOffset(ubiquitiEvent.DateCreatedUtc, TimeSpan.Zero);

        switch (ubiquitiEvent.UbiquitiEventType)
        {
            case UbiquitiEventType.Motion:
                entity[nameof(UbiquitiSnapshotEntity.LastMotionUtc)] = utc;
                entity[nameof(UbiquitiSnapshotEntity.MotionCount)] = Interlocked.Increment(ref _motionCount);
                break;
            case UbiquitiEventType.SmartDetectPerson:
                entity[nameof(UbiquitiSnapshotEntity.LastSmartDetectPersonUtc)] = utc;
                entity[nameof(UbiquitiSnapshotEntity.SmartDetectPersonCount)] = Interlocked.Increment(ref _smartDetectPersonCount);
                break;
            case UbiquitiEventType.SmartDetectVehicle:
                entity[nameof(UbiquitiSnapshotEntity.LastSmartDetectVehicleUtc)] = utc;
                entity[nameof(UbiquitiSnapshotEntity.SmartDetectVehicleCount)] = Interlocked.Increment(ref _smartDetectVehicleCount);
                break;
            case UbiquitiEventType.SmartDetectAnimal:
                entity[nameof(UbiquitiSnapshotEntity.LastSmartDetectAnimalUtc)] = utc;
                entity[nameof(UbiquitiSnapshotEntity.SmartDetectAnimalCount)] = Interlocked.Increment(ref _smartDetectAnimalCount);
                break;
            case UbiquitiEventType.SmartDetectPackage:
                entity[nameof(UbiquitiSnapshotEntity.LastSmartDetectPackageUtc)] = utc;
                entity[nameof(UbiquitiSnapshotEntity.SmartDetectPackageCount)] = Interlocked.Increment(ref _smartDetectPackageCount);
                break;
            case UbiquitiEventType.Ring:
                entity[nameof(UbiquitiSnapshotEntity.LastRingUtc)] = utc;
                entity[nameof(UbiquitiSnapshotEntity.RingCount)] = Interlocked.Increment(ref _ringCount);
                break;
        }

        return entity;
    }

    #endregion
}
