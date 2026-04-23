using Iot.Device.Hcsr501;
using System.Device.Gpio;

namespace CasCap.Services;

/// <summary>
/// Hcsr501 is a motion sensor.
/// </summary>
public class GpioHcsr501SensorService : IMotionDetectionDevice
{
    private readonly ILogger _logger;
    private readonly EdgeHardwareConfig _edgeHardwareConfig;

    private readonly Hcsr501 _sensor;

    /// <summary>Initializes a new instance of the <see cref="GpioHcsr501SensorService"/> class.</summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="edgeHardwareConfig">Edge hardware configuration.</param>
    public GpioHcsr501SensorService(ILogger<GpioHcsr501SensorService> logger, IOptions<EdgeHardwareConfig> edgeHardwareConfig)
    {
        _logger = logger;
        _edgeHardwareConfig = edgeHardwareConfig.Value;

        //https://github.com/dotnet/iot/tree/master/src/devices/Hcsr501
        _sensor = new Hcsr501(_edgeHardwareConfig.Sensors.HcSr501.OutPin, new GpioController());
        _sensor.Hcsr501ValueChanged += OnHcsr501ValueChanged;
    }

    /// <inheritdoc/>
    public event EventHandler<MotionDetectedEventArgs>? MotionDetectedEvent;

    /// <summary>Raises the <see cref="MotionDetectedEvent"/>.</summary>
    /// <param name="args">Motion detection event arguments.</param>
    protected virtual void OnRaiseMotionDetectedEvent(MotionDetectedEventArgs args) { MotionDetectedEvent?.Invoke(this, args); }

    private void OnHcsr501ValueChanged(object sender, Hcsr501ValueChangedEventArgs e)
    {
        if (e.PinValue == PinValue.High)
            OnRaiseMotionDetectedEvent(new MotionDetectedEventArgs { MotionStarted = true });
        else
            OnRaiseMotionDetectedEvent(new MotionDetectedEventArgs { MotionStopped = true });
    }

    /// <inheritdoc/>
    public bool IsMotionDetected { get { return _sensor.IsMotionDetected; } }
}
