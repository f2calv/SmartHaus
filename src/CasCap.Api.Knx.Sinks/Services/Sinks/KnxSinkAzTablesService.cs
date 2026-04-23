namespace CasCap.Services;

/// <summary>
/// Event sink that persists <see cref="KnxEvent"/> instances to Azure Table Storage.
/// Individual events are written to a line-items table and a row-per-group-address snapshot
/// is maintained in a separate table.
/// </summary>
[SinkType("AzureTables")]
public class KnxSinkAzTablesService : IEventSink<KnxEvent>
{
    private readonly ILogger _logger;
    private readonly TableClient _lineItemTableClient;
    private readonly TableClient _snapshotTableClient;

    private const string SnapshotPartitionKey = "summary";

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _snapshotInitialized;
    private ConcurrentDictionary<string, KnxSnapshotEntity> _dSnapshot = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="KnxSinkAzTablesService"/> class.
    /// </summary>
    public KnxSinkAzTablesService(ILogger<KnxSinkAzTablesService> logger, IOptions<KnxConfig> config,
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
    }

    /// <inheritdoc/>
    public async Task WriteEvent(KnxEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("{ClassName} {@KnxTelegram}", nameof(KnxSinkAzTablesService), @event);

        await EnsureSnapshotInitializedAsync(cancellationToken);

        var snapshotEntity = _dSnapshot.AddOrUpdate(@event.Kga.Name, new KnxSnapshotEntity(SnapshotPartitionKey, @event, 1), (k, v) =>
        {
            v = new KnxSnapshotEntity(SnapshotPartitionKey, @event, v.c + 1);
            return v;
        });

        var lineItemEntity = new KnxReadingEntity(@event).GetEntity();
        var lineItemTask = _lineItemTableClient.UpsertEntityAsync(lineItemEntity, cancellationToken: cancellationToken);
        var snapshotTask = _snapshotTableClient.UpsertEntityAsync(snapshotEntity.GetEntity(), cancellationToken: cancellationToken);

        try
        {
            await Task.WhenAll(lineItemTask, snapshotTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName} {MethodName} failure", nameof(KnxSinkAzTablesService), nameof(WriteEvent));
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KnxEvent> GetEvents(string? id = null, int limit = 1000, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        AsyncPageable<KnxSnapshotEntity> entities;
        if (id is null)
            entities = _snapshotTableClient.QueryAsync<KnxSnapshotEntity>(cancellationToken: cancellationToken);
        else
            entities = _snapshotTableClient.QueryAsync<KnxSnapshotEntity>(ent => ent.RowKey == id, cancellationToken: cancellationToken);
        await foreach (var entity in entities)
        {
            var args = new KnxGroupEvent { SourceAddress = new KnxSourceAddress { IndividualAddress = entity.ia } };
            var kga = new KnxGroupAddressParsed { Name = entity.RowKey, GroupAddress = entity.ga };
            yield return new KnxEvent(entity.TimestampUtc, args, kga, default!, entity.v, entity.s);
        }
    }

    /// <summary>
    /// Removes snapshot entries for group addresses not in <paramref name="validIds"/>.
    /// </summary>
    public async Task HousekeepingAsync(IReadOnlyCollection<string> validIds, CancellationToken cancellationToken = default)
    {
        var validSet = validIds as IReadOnlySet<string> ?? new HashSet<string>(validIds);
        var entitiesToDelete = new List<string>();
        await foreach (var entity in _snapshotTableClient.QueryAsync<KnxSnapshotEntity>(select: [nameof(KnxSnapshotEntity.RowKey)], cancellationToken: cancellationToken))
        {
            if (!validSet.Contains(entity.RowKey))
                entitiesToDelete.Add(entity.RowKey);
        }
        foreach (var name in entitiesToDelete)
            await _snapshotTableClient.DeleteEntityAsync(SnapshotPartitionKey, name, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Seeds the in-memory snapshot dictionary from Table Storage on first use.
    /// Uses a <see cref="SemaphoreSlim"/> to ensure only one caller reads;
    /// subsequent callers skip the check via the volatile flag.
    /// </summary>
    private async Task EnsureSnapshotInitializedAsync(CancellationToken cancellationToken)
    {
        if (_snapshotInitialized)
            return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_snapshotInitialized)
                return;

            var snapshotEntities = await _snapshotTableClient.QueryAsync<KnxSnapshotEntity>(cancellationToken: cancellationToken).ToListAsync(cancellationToken);
            _dSnapshot = new ConcurrentDictionary<string, KnxSnapshotEntity>(snapshotEntities.ToDictionary(k => k.Id, v => v));
            _snapshotInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
