namespace CasCap.Services;

/// <summary>
/// Background service that initializes all registered <see cref="IEventSink{HubEvent}"/>
/// instances when the <c>SignalRHub</c> feature starts.
/// </summary>
public class HausHubSinksBgService(ILogger<HausHubSinksBgService> logger,
    IEnumerable<IEventSink<HubEvent>> sinks) : IBgFeature
{
    /// <inheritdoc/>
    public string FeatureName => FeatureNames.SignalRHub;

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} starting — initializing {Count} hub event sink(s)",
            nameof(HausHubSinksBgService), sinks.Count());
        try
        {
            foreach (var sink in sinks)
                await sink.InitializeAsync(cancellationToken);

            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not TaskCanceledException) { throw; }
        logger.LogInformation("{ClassName} exiting", nameof(HausHubSinksBgService));
    }
}
