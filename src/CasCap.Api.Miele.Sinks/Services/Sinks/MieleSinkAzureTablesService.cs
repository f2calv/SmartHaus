namespace CasCap.Services;

/// <summary>Persists <see cref="MieleEvent"/> data to Azure Table Storage (line items + snapshot).</summary>
[SinkType("AzureTables")]
public sealed partial class MieleSinkAzureTablesService : IEventSink<MieleEvent>, IMieleQuery
{
    /// <inheritdoc/>
    public string SinkType => "AzureTables";

    private readonly ILogger _logger;
    private readonly TableClient _lineItemTableClient;
    private readonly TableClient _snapshotTableClient;
    private const string SnapshotPartitionKey = "summary";

    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance.</summary>
    public MieleSinkAzureTablesService(ILogger<MieleSinkAzureTablesService> logger,
        IOptions<AzureAuthConfig> azureAuthConfig,
        IOptions<MieleConfig> mieleConfig,
        TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
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
        LogWriteEvent(_logger, nameof(MieleSinkAzureTablesService), @event.DeviceId);

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

    [LoggerMessage(Level = LogLevel.Trace, Message = "{ClassName} writing event for device {DeviceId}")]
    private static partial void LogWriteEvent(ILogger logger, string className, string deviceId);
}
