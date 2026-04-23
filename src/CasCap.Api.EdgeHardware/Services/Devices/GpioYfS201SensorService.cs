using System.Device.Gpio;

namespace CasCap.Services;

/// <summary>
/// YF-S201 is a flow sensor.
/// </summary>
public class GpioYfS201SensorService(ILogger<GpioYfS201SensorService> logger, IKubeAppConfig kubeAppConfig)
{
    private readonly GpioController _controller = new();

    /// <summary>Starts the flow sensor monitoring loop.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} starting", nameof(GpioYfS201SensorService));
        await ReadFromSensor(cancellationToken);
        logger.LogInformation("{ClassName} exiting", nameof(GpioYfS201SensorService));
    }

    private readonly int PinNumber = 17;

    /// <summary>
    /// Total number of pulses per Minute.
    /// </summary>
    private long pulseCount;

    /// <summary>
    /// Total number of pulses.
    /// </summary>
    private long pulseCountTotal;

    /// <summary>
    /// Running total of throughput.
    /// </summary>
    private double totalLitres;

    /// <summary>
    /// Baseline calculation;
    /// 7.2 litres in 50 seconds, 3077 pulses;
    /// - 0.144 litres per second
    /// - 427 pulses per litre
    /// </summary>
    const int PulsesPerLiterConstant = 427;//Note: official data sheet says 450 pulses per minute

    private async Task ReadFromSensor(CancellationToken cancellationToken)
    {
        logger.LogInformation("{ClassName} PIN {PinNumber} on {NodeName} trying to open...",
            nameof(GpioYfS201SensorService), PinNumber, kubeAppConfig.NodeName);
        _controller.OpenPin(PinNumber, PinMode.InputPullUp);//is this the right PinMode?
        logger.LogInformation("{ClassName} PIN {PinNumber} on {NodeName} opened!",
            nameof(GpioYfS201SensorService), PinNumber, kubeAppConfig.NodeName);

        var pinValue = _controller.Read(PinNumber);
        logger.LogInformation("{ClassName} pin {PinNumber} initial status {Status} on {NodeName}",
            nameof(GpioYfS201SensorService), PinNumber, pinValue == PinValue.High ? "Rising" : "Falling", kubeAppConfig.NodeName);

        _controller.RegisterCallbackForPinValueChangedEvent(
            PinNumber,
            //PinEventTypes.Falling | PinEventTypes.Rising,
            PinEventTypes.Rising,
            PinValueChangedEvent);

        while (!cancellationToken.IsCancellationRequested)
        {
            //reset variables
            pulseCount = 0;
            monitorStartDateUtc = DateTime.UtcNow;

            //get some data
            logger.LogDebug("{ClassName} recording pulses for next {SamplingPeriodInSeconds} second(s)",
                nameof(GpioYfS201SensorService), samplingPeriodInSeconds);
            await Task.Delay(samplingPeriodInSeconds * 1000, cancellationToken);

            //calculate the frequency (i.e. pulses per second)
            var avgPulsesPerSecond = pulseCount / (DateTime.UtcNow - monitorStartDateUtc).TotalSeconds;
            //calculate the volume flow in litres per second
            var VolumeFlowLitersPerSecond = avgPulsesPerSecond / PulsesPerLiterConstant;
            var VolumeFlowLitersPerMinute = VolumeFlowLitersPerSecond * 60;
            var VolumeFlowLitersPerHour = VolumeFlowLitersPerMinute * 60;
            totalLitres += VolumeFlowLitersPerSecond * samplingPeriodInSeconds;
            logger.LogInformation("{ClassName} flow rate {PerSecond:0.0} litres/second / {PerMinute:0.0} litres/minute / {PerHour:0.0} litres/hour (pulseCount={PulseCount}, pulseCountTotal={PulseCountTotal:N2}, avgPulsesPerSecond={AvgPulsesPerSecond}, totalLitres={TotalLitres:0.0})",
                nameof(GpioYfS201SensorService), VolumeFlowLitersPerSecond, VolumeFlowLitersPerMinute, VolumeFlowLitersPerHour, pulseCount, pulseCountTotal, avgPulsesPerSecond, totalLitres);
        }
    }

    private readonly int samplingPeriodInSeconds = 15;

    private DateTime monitorStartDateUtc = DateTime.UtcNow;

    private void PinValueChangedEvent(object sender, PinValueChangedEventArgs args)
    {
        logger.LogTrace("{ClassName} pin {PinNumber} new status {Status} on {NodeName}",
            nameof(GpioYfS201SensorService), PinNumber, _controller.Read(PinNumber) == PinValue.High ? "Rising" : "Falling", kubeAppConfig.NodeName);
        pulseCount++;
        pulseCountTotal++;
    }
}
