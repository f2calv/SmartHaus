namespace CasCap.Services;

/// <summary>Persists <see cref="MieleEvent"/> data to Azure Table Storage (line items + snapshot).</summary>
[SinkType("AzureTables")]
public class MieleSinkAzTablesService : IEventSink<MieleEvent>, IMieleQuery
{
    private readonly ILogger _logger;
    private readonly TableClient _lineItemTableClient;
    private readonly TableClient _snapshotTableClient;
    private const string SnapshotPartitionKey = "summary";

    /// <summary>Initializes a new instance.</summary>
    public MieleSinkAzTablesService(ILogger<MieleSinkAzTablesService> logger,
        IOptions<AzureAuthConfig> azureAuthConfig,
        IOptions<MieleConfig> mieleConfig)
    {
        _logger = logger;
        var config = mieleConfig.Value;
        var sinkSettings = config.Sinks.AvailableSinks["AzureTables"];
        var lineItemTableName = sinkSettings.GetSetting(SinkSettingKeys.LineItemTableName) ?? "MieleReadings";
        var snapshotTableName = sinkSettings.GetSetting(SinkSettingKeys.SnapshotTableName) ?? "MieleSnapshots";
        var connectionString = config.AzureTableStorageConnectionString!;

        _lineItemTableClient = new TableClient(new Uri(connectionString), lineItemTableName, azureAuthConfig.Value.TokenCredential);
        _snapshotTableClient = new TableClient(new Uri(connectionString), snapshotTableName, azureAuthConfig.Value.TokenCredential);
    }

    /// <inheritdoc/>
    public async Task WriteEvent(MieleEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("{ClassName} {@MieleEvent}", nameof(MieleSinkAzTablesService), @event);

        var lineItemEntity = new MieleReadingEntity(@event).GetEntity();
        var snapshotEntity = new MieleSnapshotEntity(SnapshotPartitionKey, @event).GetEntity();

        await Task.WhenAll(
            _lineItemTableClient.UpsertEntityAsync(lineItemEntity, cancellationToken: cancellationToken),
            _snapshotTableClient.UpsertEntityAsync(snapshotEntity, cancellationToken: cancellationToken));
    }

    /// <inheritdoc/>
    public async Task<List<MieleSnapshot>> GetSnapshots()
    {
        var snapshots = new List<MieleSnapshot>();
        await foreach (var entity in _snapshotTableClient.QueryAsync<MieleSnapshotEntity>(
            e => e.PartitionKey == SnapshotPartitionKey))
        {
            snapshots.Add(new MieleSnapshot
            {
                DeviceId = entity.DeviceId,
                DeviceName = entity.DeviceName,
                StatusCode = entity.StatusCode,
                ProgramId = entity.ProgramId,
                ErrorCode = entity.ErrorCode,
                ReadingUtc = entity.ReadingUtc,
            });
        }
        return snapshots;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<MieleEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var partitionKey = DateTime.UtcNow.ToString("yyMMdd");
        var count = 0;
        await foreach (var entity in _lineItemTableClient.QueryAsync<MieleReadingEntity>(
            e => e.PartitionKey == partitionKey, cancellationToken: cancellationToken))
        {
            if (count++ >= limit) yield break;
            yield return new MieleEvent
            {
                DeviceId = entity.did,
                EventType = (MieleEventType)entity.et,
                StatusCode = entity.sc,
                ProgramId = entity.pid,
                ErrorCode = entity.ec,
                TimestampUtc = entity.TimestampUtc,
            };
        }
    }
}
