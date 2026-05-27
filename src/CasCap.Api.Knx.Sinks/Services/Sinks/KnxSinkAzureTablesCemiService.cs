namespace CasCap.Services;

/// <summary>
/// Sink that serialises each <see cref="KnxEvent"/> to a raw CEMI frame
/// and writes it to Azure Table Storage. When batching is enabled (the default),
/// entities are queued and flushed in batches for improved efficiency.
/// </summary>
/// <remarks>
/// Batching is controlled by the <see cref="SinkSettingKeys.BatchingEnabled"/> setting
/// (defaults to <see langword="true"/>). When enabled, entities are queued via <see cref="WriteEvent"/>
/// and flushed by a background loop started in <see cref="InitializeAsync"/>.
/// Azure Table Storage supports a maximum of 100 entities per transaction,
/// all sharing the same partition key.
/// </remarks>
[SinkType("AzureTablesCemi")]
public partial class KnxSinkAzureTablesCemiService : IEventSink<KnxEvent>
{
    /// <inheritdoc/>
    public string SinkType => "AzureTablesCemi";

    private readonly ILogger _logger;
    private readonly TableClient _cemiTableClient;
    private readonly bool _batchingEnabled;
    private readonly Channel<TableEntity> _batchChannel = Channel.CreateBounded<TableEntity>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.Wait });

    private const string CemiPartitionKey = "summary";
    private const int MaxBatchSize = 100;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Initializes a new instance of the <see cref="KnxSinkAzureTablesCemiService"/> class.
    /// </summary>
    public KnxSinkAzureTablesCemiService(ILogger<KnxSinkAzureTablesCemiService> logger, IOptions<KnxConfig> config,
        IOptions<AzureAuthConfig> azureAuthConfig)
    {
        _logger = logger;

        var cemiConfig = config.Value.Sinks.AvailableSinks["AzureTablesCemi"];
        var connectionString = config.Value.AzureTableStorageConnectionString;
        var cemiTableName = cemiConfig.GetSetting(KnxSinkKeys.CemiTableName)!;
        _cemiTableClient = StorageExtensions.CreateTableClient(connectionString, cemiTableName, azureAuthConfig.Value.TokenCredential);
        _cemiTableClient.CreateIfNotExists();

        var batchingSetting = cemiConfig.GetSetting(SinkSettingKeys.BatchingEnabled);
        _batchingEnabled = !bool.TryParse(batchingSetting, out var parsed) || parsed;
    }

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (_batchingEnabled)
        {
            _logger.LogInformation("{ClassName} batching enabled, starting flush loop", nameof(KnxSinkAzureTablesCemiService));
            _ = Task.Run(() => FlushLoopAsync(cancellationToken), cancellationToken);
        }
        else
        {
            _logger.LogInformation("{ClassName} batching disabled, writes will be submitted individually",
                nameof(KnxSinkAzureTablesCemiService));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task WriteEvent(KnxEvent @event, CancellationToken cancellationToken = default)
    {
        LogSerialisingCemi(_logger, nameof(KnxSinkAzureTablesCemiService), @event.Kga.Name);

        var cemiHex = @event.Args.ToCemiHex();
        var entity = new KnxCemiReadingEntity(CemiPartitionKey, @event, cemiHex).GetEntity();

        if (_batchingEnabled)
        {
            await _batchChannel.Writer.WriteAsync(entity, cancellationToken);
        }
        else
        {
            try
            {
                await _cemiTableClient.UpsertEntityAsync(entity, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                LogWriteCemiFailed(_logger, ex, nameof(KnxSinkAzureTablesCemiService), @event.Kga.Name);
            }
        }
    }


    /// <summary>
    /// Retrieves all CEMI reading entities from the table, ordered by timestamp (RowKey).
    /// </summary>
    public async Task<List<KnxCemiReadingEntity>> GetCemiReadingEntities(CancellationToken cancellationToken)
        => await _cemiTableClient.QueryAsync<KnxCemiReadingEntity>(cancellationToken: cancellationToken).ToListAsync();

    #region private helpers

    /// <summary>
    /// Background loop that drains the channel and submits entities in batches.
    /// Flushes either when <see cref="MaxBatchSize"/> entities are buffered or
    /// every <see cref="FlushInterval"/>, whichever comes first.
    /// </summary>
    private async Task FlushLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("{ClassName} batch flush loop started", nameof(KnxSinkAzureTablesCemiService));

        var batch = new List<TableTransactionAction>(MaxBatchSize);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timerCts.CancelAfter(FlushInterval);

                try
                {
                    while (batch.Count < MaxBatchSize)
                    {
                        var entity = await _batchChannel.Reader.ReadAsync(timerCts.Token);
                        batch.Add(new TableTransactionAction(TableTransactionActionType.UpsertReplace, entity));
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Timer expired — flush whatever we have
                }

                if (batch.Count > 0)
                    await FlushBatchAsync(batch, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{ClassName} unexpected error in flush loop", nameof(KnxSinkAzureTablesCemiService));
                await Task.Delay(1_000, cancellationToken);
            }
        }

        // Drain any remaining items on shutdown
        while (_batchChannel.Reader.TryRead(out var remaining))
            batch.Add(new TableTransactionAction(TableTransactionActionType.UpsertReplace, remaining));

        if (batch.Count > 0)
            await FlushBatchAsync(batch, CancellationToken.None);

        _logger.LogInformation("{ClassName} batch flush loop stopped", nameof(KnxSinkAzureTablesCemiService));
    }

    /// <summary>
    /// Submits a batch of entities to Azure Table Storage and clears the batch list.
    /// </summary>
    private async Task FlushBatchAsync(List<TableTransactionAction> batch, CancellationToken cancellationToken)
    {
        try
        {
            LogFlushingBatch(_logger, nameof(KnxSinkAzureTablesCemiService), batch.Count);
            await _cemiTableClient.SubmitTransactionAsync(batch, cancellationToken);
        }
        catch (Exception ex)
        {
            LogFlushBatchFailed(_logger, ex, nameof(KnxSinkAzureTablesCemiService), batch.Count);
        }
        finally
        {
            batch.Clear();
        }
    }

    #endregion

    [LoggerMessage(Level = LogLevel.Trace, Message = "{ClassName} serialising CEMI for {GroupAddressName}")]
    private static partial void LogSerialisingCemi(ILogger logger, string className, string groupAddressName);

    [LoggerMessage(Level = LogLevel.Error, Message = "{ClassName} failed to write CEMI data for {GroupAddressName}")]
    private static partial void LogWriteCemiFailed(ILogger logger, Exception ex, string className, string groupAddressName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{ClassName} flushing {Count} CEMI entities")]
    private static partial void LogFlushingBatch(ILogger logger, string className, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "{ClassName} failed to submit batch of {Count} CEMI entities")]
    private static partial void LogFlushBatchFailed(ILogger logger, Exception ex, string className, int count);
}
