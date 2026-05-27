namespace CasCap.Services;

/// <summary>
/// Event sink that persists <see cref="BuderusEvent"/> instances to Azure Table Storage.
/// Individual events are written to a line-items table and a single running snapshot row
/// is maintained via merge-upsert — each datapoint becomes a column.
/// </summary>
[SinkType("AzureTables")]
public partial class BuderusSinkAzTablesService : IEventSink<BuderusEvent>, IBuderusQuery
{
    /// <inheritdoc/>
    public string SinkType => "AzureTables";

    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly TableClient _lineItemTableClient;
    private readonly TableClient _snapshotTableClient;
    private readonly Dictionary<string, DatapointMapping> _datapointMappings;

    private const string SnapshotPartitionKey = "summary";
    private const string SnapshotRowKey = "latest";

    /// <summary>
    /// Initializes a new instance of the <see cref="BuderusSinkAzTablesService"/> class.
    /// </summary>
    public BuderusSinkAzTablesService(ILogger<BuderusSinkAzTablesService> logger, IOptions<BuderusConfig> config,
        IOptions<AzureAuthConfig> azureAuthConfig, TimeProvider timeProvider)
    {
        _logger = logger;
        _timeProvider = timeProvider;

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
        LogWriteEvent(_logger, nameof(BuderusSinkAzTablesService), @event.Id);

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
    public Task<BuderusSnapshot> GetSnapshot()
        => GetSnapshotFromTable();

    /// <inheritdoc/>
    public async IAsyncEnumerable<BuderusEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var pk = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyMMdd");
        var filter = id is not null
            ? $"PartitionKey eq '{pk}' and RowKey ge '{id}' and RowKey lt '{id}~'"
            : $"PartitionKey eq '{pk}'";
        var count = 0;
        await foreach (var entity in _lineItemTableClient.QueryAsync<TableEntity>(filter, maxPerPage: limit, cancellationToken: cancellationToken))
        {
            if (++count > limit) yield break;
            var datapointId = entity.GetString("Id") ?? entity.RowKey;
            var value = entity.GetString("v") ?? string.Empty;
            var ts = entity.GetDateTimeOffset("Timestamp") ?? DateTimeOffset.MinValue;
            yield return new BuderusEvent(datapointId, value, ts.UtcDateTime);
        }
    }

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

    [LoggerMessage(Level = LogLevel.Trace, Message = "{ClassName} writing event for datapoint {DatapointId}")]
    private static partial void LogWriteEvent(ILogger logger, string className, string datapointId);
}
