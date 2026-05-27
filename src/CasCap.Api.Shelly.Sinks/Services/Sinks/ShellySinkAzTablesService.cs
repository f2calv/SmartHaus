namespace CasCap.Services;

/// <summary>
/// Event sink that persists <see cref="ShellyEvent"/> instances to Azure Table Storage.
/// Individual events are written to a line-items table and a per-device snapshot row is upserted.
/// </summary>
[SinkType("AzureTables")]
public partial class ShellySinkAzTablesService : IEventSink<ShellyEvent>, IShellyQuery
{
    /// <inheritdoc/>
    public string SinkType => "AzureTables";

    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly TableClient _lineItemTableClient;
    private readonly TableClient _snapshotTableClient;

    private const string SnapshotPartitionKey = "summary";

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellySinkAzTablesService"/> class.
    /// </summary>
    public ShellySinkAzTablesService(ILogger<ShellySinkAzTablesService> logger,
        IOptions<AzureAuthConfig> azureAuthConfig,
        IOptions<ShellyConfig> config,
        TimeProvider timeProvider)
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
    }

    /// <inheritdoc/>
    public async Task WriteEvent(ShellyEvent @event, CancellationToken cancellationToken = default)
    {
        LogWriteEvent(_logger, nameof(ShellySinkAzTablesService), @event.DeviceId);

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

    /// <inheritdoc/>
    public async IAsyncEnumerable<ShellyEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var pk = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyMMdd");
        var filter = id is not null
            ? $"PartitionKey eq '{pk}' and RowKey ge '{id}' and RowKey lt '{id}~'"
            : $"PartitionKey eq '{pk}'";
        var count = 0;
        await foreach (var entity in _lineItemTableClient.QueryAsync<ShellyReadingEntity>(filter, maxPerPage: limit, cancellationToken: cancellationToken))
        {
            if (++count > limit) yield break;
            yield return new ShellyEvent(
                deviceId: entity.DeviceId ?? string.Empty,
                deviceName: string.Empty,
                power: entity.Power,
                relayState: entity.RelayState,
                temperature: entity.Temperature,
                overpower: entity.Overpower,
                entity.Timestamp?.UtcDateTime ?? DateTime.MinValue);
        }
    }

    /// <summary>
    /// Retrieves smart plug line item readings for the current day.
    /// </summary>
    /// <param name="limit">Maximum number of records to return. Default 100, maximum 1000.</param>
    public async Task<IEnumerable<ShellyReadingEntity>> GetReadings(int limit = 100)
    {
        var partitionKey = _timeProvider.GetUtcNow().UtcDateTime.ToString("yyMMdd");
        _logger.LogInformation("{ClassName} Getting data from table storage for partitionKey '{PartitionKey}'",
            nameof(ShellySinkAzTablesService), partitionKey);
        var entities = await _lineItemTableClient.QueryAsync<ShellyReadingEntity>(
            p => p.PartitionKey == partitionKey
            ).ToListAsync();
        return entities.OrderByDescending(p => p.RowKey).Take(Math.Min(limit, 1000));
    }

    [LoggerMessage(Level = LogLevel.Trace, Message = "{ClassName} writing event for device {DeviceId}")]
    private static partial void LogWriteEvent(ILogger logger, string className, string deviceId);
}
