namespace CasCap.Services;

/// <summary>
/// Idle background service that satisfies the <see cref="IBgFeature"/> requirement for the Ubiquiti feature.
/// The Ubiquiti integration is webhook-driven and has no active polling, but
/// <c>FeatureFlagBgService</c> requires at least one registered <see cref="IBgFeature"/>
/// per enabled feature to avoid a startup exception.
/// </summary>
public class UbiquitiBgService(ILogger<UbiquitiBgService> logger) : IBgFeature
{
    /// <inheritdoc/>
    public string FeatureName => "Ubiquiti";

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} started (webhook-only, idling)", nameof(UbiquitiBgService));
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException) { }
        logger.LogInformation("{ClassName} exiting", nameof(UbiquitiBgService));
    }
}
