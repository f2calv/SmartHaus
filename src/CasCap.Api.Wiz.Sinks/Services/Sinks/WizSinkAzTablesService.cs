namespace CasCap.Services;

/// <summary>Persists <see cref="WizEvent"/> data to Azure Table Storage (line items + snapshot).</summary>
[SinkType("AzureTables")]
public class WizSinkAzTablesService : IEventSink<WizEvent>, IWizQuery
{
    private readonly ILogger _logger;
    private readonly TableClient _lineItemTableClient;
    private readonly TableClient _snapshotTableClient;
    private const string SnapshotPartitionKey = "summary";

    /// <summary>Initializes a new instance.</summary>
    public WizSinkAzTablesService(ILogger<WizSinkAzTablesService> logger,
        IOptions<AzureAuthConfig> azureAuthConfig,
        IOptions<WizConfig> wizConfig)
    {
        _logger = logger;
        var config = wizConfig.Value;
        var sinkSettings = config.Sinks.AvailableSinks["AzureTables"];
        var lineItemTableName = sinkSettings.GetSetting(SinkSettingKeys.LineItemTableName) ?? "WizReadings";
        var snapshotTableName = sinkSettings.GetSetting(SinkSettingKeys.SnapshotTableName) ?? "WizSnapshots";
        var connectionString = config.AzureTableStorageConnectionString!;

        _lineItemTableClient = StorageExtensions.CreateTableClient(connectionString, lineItemTableName, azureAuthConfig.Value.TokenCredential);
        _snapshotTableClient = StorageExtensions.CreateTableClient(connectionString, snapshotTableName, azureAuthConfig.Value.TokenCredential);
    }

    /// <inheritdoc/>
    public async Task WriteEvent(WizEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("{ClassName} {@WizEvent}", nameof(WizSinkAzTablesService), @event);

        var lineItemEntity = new WizReadingEntity(@event).GetEntity();
        var snapshotEntity = new WizSnapshotEntity(SnapshotPartitionKey, @event).GetEntity();

        await Task.WhenAll(
            _lineItemTableClient.UpsertEntityAsync(lineItemEntity, cancellationToken: cancellationToken),
            _snapshotTableClient.UpsertEntityAsync(snapshotEntity, cancellationToken: cancellationToken));
    }

    /// <inheritdoc/>
    public Task<List<WizSnapshot>> GetSnapshots() =>
        GetSnapshotEntities();

    /// <inheritdoc/>
    public async IAsyncEnumerable<WizEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var count = 0;
        AsyncPageable<WizReadingEntity> query;
        if (id is not null)
            query = _lineItemTableClient.QueryAsync<WizReadingEntity>(
                e => e.PartitionKey == id, cancellationToken: cancellationToken);
        else
            query = _lineItemTableClient.QueryAsync<WizReadingEntity>(cancellationToken: cancellationToken);

        await foreach (var entity in query)
        {
            if (count++ >= limit) yield break;
            yield return new WizEvent
            {
                DeviceId = entity.PartitionKey,
                IpAddress = entity.ip,
                State = entity.s,
                Dimming = entity.d,
                SceneId = entity.sc,
                Temp = entity.t,
                Rssi = entity.r,
                TimestampUtc = entity.TimestampUtc,
            };
        }
    }

    #region private helpers

    private async Task<List<WizSnapshot>> GetSnapshotEntities()
    {
        var snapshots = new List<WizSnapshot>();
        await foreach (var entity in _snapshotTableClient.QueryAsync<WizSnapshotEntity>(
            e => e.PartitionKey == SnapshotPartitionKey))
        {
            snapshots.Add(new WizSnapshot
            {
                DeviceId = entity.DeviceId,
                IpAddress = entity.IpAddress,
                Mac = entity.Mac,
                State = entity.State,
                Dimming = entity.Dimming,
                SceneId = entity.SceneId,
                Temp = entity.Temp,
                Rssi = entity.Rssi,
                ReadingUtc = entity.ReadingUtc,
            });
        }
        return snapshots;
    }

    #endregion
}
