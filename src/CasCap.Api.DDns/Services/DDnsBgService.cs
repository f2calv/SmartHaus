namespace CasCap.Services;

/// <summary>Runs Dynamic DNS updates within the shared leadership lifecycle.</summary>
public sealed class DDnsBgService(
    ILogger<DDnsBgService> logger,
    ILeadershipRunner leadershipRunner,
    DDnsUpdateService dDnsUpdateSvc
    ) : IBgFeature
{
    /// <inheritdoc/>
    public string FeatureName => DDnsConfig.FeatureName;

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} starting", nameof(DDnsBgService));
        await leadershipRunner.RunAsync("ddns", dDnsUpdateSvc.RunAsync, cancellationToken);
        logger.LogInformation("{ClassName} exiting", nameof(DDnsBgService));
    }
}
