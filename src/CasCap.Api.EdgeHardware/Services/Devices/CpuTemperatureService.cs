using Iot.Device.CpuTemperature;

namespace CasCap.Services;

/// <summary>
/// Reads CPU temperature from the Linux thermal zone via <see cref="CpuTemperature"/>.
/// </summary>
public class CpuTemperatureService(ILogger<CpuTemperatureService> logger) : ICpuTemperatureProvider
{
    private readonly CpuTemperature _temperature = new();

    /// <inheritdoc/>
    public double? GetTempInCelsius()
    {
        if (_temperature.IsAvailable)
        {
            var temp = Math.Round(_temperature.Temperature.DegreesCelsius, 2);
            logger.LogTrace("{ClassName} CPU temperature is {Temperature}°C", nameof(CpuTemperatureService), temp);
            return temp;
        }
        else
        {
            logger.LogDebug("{ClassName} CPU temperature sensor not available", nameof(CpuTemperatureService));
            return null;
        }
    }
}
