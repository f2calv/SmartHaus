namespace CasCap.Services;

/// <summary>
/// Idle background service that satisfies the <see cref="IBgFeature"/> requirement for the DoorBird feature.
/// The DoorBird integration is webhook-driven and has no active polling, but
/// <c>FeatureFlagBgService</c> requires at least one registered <see cref="IBgFeature"/>
/// per enabled feature to avoid a startup exception.
/// </summary>
public class DoorBirdBgService(ILogger<DoorBirdBgService> logger) : IBgFeature
{
    /// <inheritdoc/>
    public string FeatureName => "DoorBird";

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} started (webhook-only, idling)", nameof(DoorBirdBgService));
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException) { }
        logger.LogInformation("{ClassName} exiting", nameof(DoorBirdBgService));
    }
}
