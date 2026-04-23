namespace CasCap.Abstractions;

/// <summary>
/// Provides CPU temperature readings from the current hardware.
/// </summary>
/// <remarks>
/// Implemented by <see cref="CasCap.Services.CpuTemperatureService"/> (Linux, via IoT bindings)
/// and LibreHardwareCpuTemperatureService (Windows, via LibreHardwareMonitor — conditional on WINDOWS).
/// </remarks>
public interface ICpuTemperatureProvider
{
    /// <summary>Returns the current CPU temperature in Celsius, or <see langword="null"/> when unavailable.</summary>
    double? GetTempInCelsius();
}
