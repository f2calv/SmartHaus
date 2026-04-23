namespace CasCap.Services;

/// <summary>Background service for monitoring Raspberry Pi GPIO sensors.</summary>
/// <param name="logger">Logger instance.</param>
/// <param name="kubeAppConfig">Kubernetes application configuration.</param>
/// <param name="piYfS201SensorSvc">YF-S201 flow sensor service.</param>
/// <param name="piBmp280SensorSvc">BMP280 temperature/pressure sensor service.</param>
public class GpioMonitorBgService(ILogger<GpioMonitorBgService> logger,
    IKubeAppConfig kubeAppConfig,
    GpioYfS201SensorService piYfS201SensorSvc,
    GpioBmp280SensorService piBmp280SensorSvc) : IBgFeature
{
    /// <inheritdoc/>
    public string FeatureName => "EdgeHardware";

    /// <inheritdoc/>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} starting", nameof(GpioMonitorBgService));
        if (kubeAppConfig.NodeName == "pi4-8gb")
        {
            var tasks = new List<Task>();
            tasks.Add(piYfS201SensorSvc.ExecuteAsync(cancellationToken));
            tasks.Add(piBmp280SensorSvc.ExecuteAsync(cancellationToken));
            await Task.WhenAll(tasks);
        }
        else
        {
            logger.LogWarning("{ClassName} this node {NodeName} has no GPIO, idling",
                nameof(GpioMonitorBgService), kubeAppConfig.NodeName);
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException) { }
        }
        logger.LogInformation("{ClassName} exiting", nameof(GpioMonitorBgService));
    }
}
