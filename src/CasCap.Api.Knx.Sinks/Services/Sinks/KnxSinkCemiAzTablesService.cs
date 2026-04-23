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
public class KnxSinkCemiAzTablesService : IEventSink<KnxEvent>
{
    private readonly ILogger _logger;
    private readonly TableClient _cemiTableClient;
    private readonly bool _batchingEnabled;
    private readonly Channel<TableEntity> _batchChannel = Channel.CreateBounded<TableEntity>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.Wait });

    private const string CemiPartitionKey = "summary";
    private const int MaxBatchSize = 100;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Initializes a new instance of the <see cref="KnxSinkCemiAzTablesService"/> class.
    /// </summary>
    public KnxSinkCemiAzTablesService(ILogger<KnxSinkCemiAzTablesService> logger, IOptions<KnxConfig> config,
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
            _logger.LogInformation("{ClassName} batching enabled, starting flush loop", nameof(KnxSinkCemiAzTablesService));
            _ = Task.Run(() => FlushLoopAsync(cancellationToken), cancellationToken);
        }
        else
        {
            _logger.LogInformation("{ClassName} batching disabled, writes will be submitted individually",
                nameof(KnxSinkCemiAzTablesService));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task WriteEvent(KnxEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("{ClassName} serialising CEMI for '{GroupAddressName}'",
            nameof(KnxSinkCemiAzTablesService), @event.Kga.Name);

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
                _logger.LogError(ex, "{ClassName} failed to write CEMI data for '{GroupAddressName}'",
                    nameof(KnxSinkCemiAzTablesService), @event.Kga.Name);
            }
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<KnxEvent> GetEvents(string? id = null, int limit = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(0, cancellationToken);
        throw new NotSupportedException();
        yield return null;
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
        _logger.LogInformation("{ClassName} batch flush loop started", nameof(KnxSinkCemiAzTablesService));

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
                _logger.LogError(ex, "{ClassName} unexpected error in flush loop", nameof(KnxSinkCemiAzTablesService));
                await Task.Delay(1_000, cancellationToken);
            }
        }

        // Drain any remaining items on shutdown
        while (_batchChannel.Reader.TryRead(out var remaining))
            batch.Add(new TableTransactionAction(TableTransactionActionType.UpsertReplace, remaining));

        if (batch.Count > 0)
            await FlushBatchAsync(batch, CancellationToken.None);

        _logger.LogInformation("{ClassName} batch flush loop stopped", nameof(KnxSinkCemiAzTablesService));
    }

    /// <summary>
    /// Submits a batch of entities to Azure Table Storage and clears the batch list.
    /// </summary>
    private async Task FlushBatchAsync(List<TableTransactionAction> batch, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("{ClassName} flushing {Count} CEMI entities", nameof(KnxSinkCemiAzTablesService), batch.Count);
            await _cemiTableClient.SubmitTransactionAsync(batch, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName} failed to submit batch of {Count} CEMI entities",
                nameof(KnxSinkCemiAzTablesService), batch.Count);
        }
        finally
        {
            batch.Clear();
        }
    }

    #endregion
}
