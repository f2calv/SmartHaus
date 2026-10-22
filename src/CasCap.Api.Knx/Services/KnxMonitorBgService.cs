using Knx.Falcon.KnxnetIp;

namespace CasCap.Services;

/// <summary>Runs KNX monitoring and outbound delivery within one shared leadership lifetime.</summary>
public sealed class KnxMonitorBgService(
    ILogger<KnxMonitorBgService> logger,
    IOptions<KnxConfig> config,
    KnxGroupAddressLookupService knxGroupAddressLookupSvc,
    ILeadershipRunner leadershipRunner,
    Func<IKnxMonitor> monitorFactory,
    KnxSenderBgService knxSenderBgSvc) : IBgFeature
{
    /// <inheritdoc/>
    public string FeatureName => "Knx";

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await knxGroupAddressLookupSvc.WaitUntilReadyAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("{ClassName} ServiceFamily={ServiceFamily}, ShardingMode={ShardingMode}, TelegramBrokerMode={TelegramBrokerMode}",
            nameof(KnxMonitorBgService), config.Value.ServiceFamily, config.Value.ShardingMode, config.Value.TelegramBrokerMode);

        var effectiveShardingMode = config.Value.ServiceFamily == ServiceFamily.Routing
            ? ShardingMode.Unified
            : config.Value.ShardingMode;
        if (effectiveShardingMode != config.Value.ShardingMode)
            logger.LogWarning("{ClassName} ShardingMode overridden to {EffectiveShardingMode} because ServiceFamily is {ServiceFamily}",
                nameof(KnxMonitorBgService), effectiveShardingMode, config.Value.ServiceFamily);
        if (effectiveShardingMode != ShardingMode.Unified)
            throw new NotSupportedException(
                $"{nameof(KnxConfig.ShardingMode)} '{effectiveShardingMode}' is not supported. Use {nameof(ShardingMode.Unified)}.");

        await leadershipRunner.RunAsync("knx:monitor", RunOwnedAsync, cancellationToken).ConfigureAwait(false);
    }

    private async Task RunOwnedAsync(CancellationToken cancellationToken)
    {
        await using var monitor = monitorFactory();
        await monitor.ConnectAsync(cancellationToken).ConfigureAwait(false);

        Func<CancellationToken, Task>[] workers = [monitor.RunAsync, knxSenderBgSvc.ExecuteAsync];
        await Parallel.ForEachAsync(workers,
            new ParallelOptions { MaxDegreeOfParallelism = workers.Length, CancellationToken = cancellationToken },
            async (worker, token) =>
            {
                await worker(token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                throw new InvalidOperationException("A KNX ownership worker stopped unexpectedly.");
            }).ConfigureAwait(false);
    }
}
