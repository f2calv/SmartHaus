#if WINDOWS
using LibreHardwareMonitor.Hardware;

namespace CasCap.Services;

/// <summary>
/// Reads CPU temperature on Windows via <see cref="Computer"/> from LibreHardwareMonitor.
/// </summary>
/// <remarks>
/// Intended for local development on Windows machines where
/// <see cref="Iot.Device.CpuTemperature.CpuTemperature"/> (Linux thermal zone) is unavailable.
/// Requires administrator privileges for hardware access.
/// </remarks>
public sealed class LibreHardwareCpuTemperatureService(ILogger<LibreHardwareCpuTemperatureService> logger) : ICpuTemperatureProvider, IDisposable
{
    private readonly Computer _computer = InitComputer(logger);
    private readonly HardwareUpdateVisitor _visitor = new();

    /// <inheritdoc/>
    public double? GetTempInCelsius()
    {
        try
        {
            _computer.Accept(_visitor);
            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType != HardwareType.Cpu)
                    continue;

                // Prefer the "CPU Package" sensor (Intel) or "Core (Tctl/Tdie)" (AMD)
                ISensor? fallback = null;
                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType != SensorType.Temperature || !sensor.Value.HasValue)
                        continue;

                    if (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)
                        || sensor.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase))
                        return Math.Round(sensor.Value.Value, 2);

                    fallback ??= sensor;
                }

                if (fallback?.Value is not null)
                    return Math.Round(fallback.Value.Value, 2);

                logger.LogWarning("{ClassName} CPU hardware found but no temperature sensors — ensure the process is running with administrator privileges",
                    nameof(LibreHardwareCpuTemperatureService));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{ClassName} failed to read CPU temperature", nameof(LibreHardwareCpuTemperatureService));
        }
        return null;
    }

    /// <inheritdoc/>
    public void Dispose() => _computer.Close();

    private static Computer InitComputer(ILogger logger)
    {
        var computer = new Computer { IsCpuEnabled = true };
        computer.Open();
        if (computer.Hardware.Count == 0)
            logger.LogWarning("{ClassName} LibreHardwareMonitor detected 0 hardware items — ensure the process is running with administrator privileges",
                nameof(LibreHardwareCpuTemperatureService));
        else
            logger.LogInformation("{ClassName} LibreHardwareMonitor opened, {Count} hardware item(s) detected",
                nameof(LibreHardwareCpuTemperatureService), computer.Hardware.Count);
        return computer;
    }

    /// <summary>Visitor that updates hardware sensor readings on traversal.</summary>
    private sealed class HardwareUpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware)
                sub.Accept(this);
        }

        public void VisitSensor(ISensor sensor) { }

        public void VisitParameter(IParameter parameter) { }
    }
}
#endif
