using CasCap.Models;

namespace CasCap.Extensions;

/// <summary>
/// Domain-specific GPU and energy metric extensions for <see cref="AgentRunResult"/>,
/// backed by <see cref="AgentRunResult.AdditionalProperties"/>.
/// </summary>
public static class AgentRunResultExtensions
{
    const string EstimatedEnergyWhKey = nameof(EstimatedEnergyWhKey);
    const string GpuPowerDrawWKey = nameof(GpuPowerDrawWKey);
    const string GpuTemperatureCKey = nameof(GpuTemperatureCKey);
    const string GpuUtilizationPercentKey = nameof(GpuUtilizationPercentKey);

    /// <summary>Gets the estimated energy consumed by this inference run in watt-hours.</summary>
    public static double? GetEstimatedEnergyWh(this AgentRunResult result) =>
        result.AdditionalProperties.TryGetValue(EstimatedEnergyWhKey, out var v) ? v as double? : null;

    /// <summary>Sets the estimated energy consumed by this inference run in watt-hours.</summary>
    public static void SetEstimatedEnergyWh(this AgentRunResult result, double? value) =>
        result.AdditionalProperties[EstimatedEnergyWhKey] = value;

    /// <summary>Gets the average GPU power draw during inference in watts.</summary>
    public static double? GetGpuPowerDrawW(this AgentRunResult result) =>
        result.AdditionalProperties.TryGetValue(GpuPowerDrawWKey, out var v) ? v as double? : null;

    /// <summary>Sets the average GPU power draw during inference in watts.</summary>
    public static void SetGpuPowerDrawW(this AgentRunResult result, double? value) =>
        result.AdditionalProperties[GpuPowerDrawWKey] = value;

    /// <summary>Gets the GPU temperature at inference completion in degrees Celsius.</summary>
    public static double? GetGpuTemperatureC(this AgentRunResult result) =>
        result.AdditionalProperties.TryGetValue(GpuTemperatureCKey, out var v) ? v as double? : null;

    /// <summary>Sets the GPU temperature at inference completion in degrees Celsius.</summary>
    public static void SetGpuTemperatureC(this AgentRunResult result, double? value) =>
        result.AdditionalProperties[GpuTemperatureCKey] = value;

    /// <summary>Gets the GPU utilization at inference completion as a percentage (0–100).</summary>
    public static double? GetGpuUtilizationPercent(this AgentRunResult result) =>
        result.AdditionalProperties.TryGetValue(GpuUtilizationPercentKey, out var v) ? v as double? : null;

    /// <summary>Sets the GPU utilization at inference completion as a percentage (0–100).</summary>
    public static void SetGpuUtilizationPercent(this AgentRunResult result, double? value) =>
        result.AdditionalProperties[GpuUtilizationPercentKey] = value;

    /// <summary>
    /// Calculates per-query energy consumption from GPU power snapshots taken
    /// before and after inference. Falls back to a token-based estimate when
    /// live GPU metrics are unavailable.
    /// </summary>
    public static void PopulateEnergyMetrics(
        this AgentRunResult result,
        EdgeHardwareSnapshot? preSnapshot,
        EdgeHardwareSnapshot? postSnapshot,
        EdgeHardwareConfig? edgeHardwareConfig)
    {
        if (postSnapshot is not null)
        {
            result.SetGpuTemperatureC(postSnapshot.GpuTemperatureC);
            result.SetGpuUtilizationPercent(postSnapshot.GpuUtilizationPercent);
        }

        // Approach A: average power × elapsed hours.
        if (preSnapshot?.GpuPowerDrawW is not null && postSnapshot?.GpuPowerDrawW is not null)
        {
            var avgPowerW = (preSnapshot.GpuPowerDrawW.Value + postSnapshot.GpuPowerDrawW.Value) / 2.0;
            result.SetGpuPowerDrawW(avgPowerW);
            result.SetEstimatedEnergyWh(avgPowerW * result.Elapsed.TotalHours);
            return;
        }

        // Approach C fallback: token-based estimate.
        if (edgeHardwareConfig is not null
            && edgeHardwareConfig.EnergyPerKiloTokenWh > 0
            && result.Usage?.TotalTokenCount is > 0)
            result.SetEstimatedEnergyWh((result.Usage.TotalTokenCount.Value / 1000.0) * edgeHardwareConfig.EnergyPerKiloTokenWh);
    }
}
