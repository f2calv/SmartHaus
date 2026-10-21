namespace CasCap.Services;

/// <summary>
/// <see cref="IAgentRunEnricher"/> that records the edge GPU's energy use for each comms agent run and
/// reports it in the monitor-group timeline and, when enabled, the reply footer.
/// </summary>
/// <remarks>
/// GPU snapshots come from <see cref="IEdgeHardwareQueryService"/> when the EdgeHardware feature is
/// registered; otherwise energy falls back to the token-based estimate in <see cref="EdgeHardwareConfig"/>.
/// Solar context comes from <see cref="IFroniusQueryService"/> when the Fronius feature is registered
/// and <see cref="EdgeHardwareConfig.SolarContextEnabled"/> is set.
/// </remarks>
public sealed class EdgeHardwareAgentRunEnricher(
    IOptions<EdgeHardwareConfig> edgeHardwareConfig,
    IServiceProvider serviceProvider) : IAgentRunEnricher
{
    /// <inheritdoc/>
    /// <remarks>Energy comes from hardware snapshots and token usage, which every run returns.</remarks>
    public bool RequiresDiagnosticDetails => false;

    /// <inheritdoc/>
    public async Task<object?> BeforeRunAsync(CancellationToken cancellationToken) =>
        await GetLatestSnapshotAsync();

    /// <inheritdoc/>
    public async Task AfterRunAsync(CommsAgentRunResult result, object? state, CancellationToken cancellationToken)
    {
        var postSnapshot = await GetLatestSnapshotAsync();
        result.PopulateEnergyMetrics(state as EdgeHardwareSnapshot, postSnapshot, edgeHardwareConfig.Value);
    }

    /// <inheritdoc/>
    public async Task<string?> FormatFooterLineAsync(CommsAgentRunResult result, CancellationToken cancellationToken)
    {
        var energyWh = result.GetEstimatedEnergyWh();
        var gpuTemp = result.GetGpuTemperatureC();
        var gpuUtil = result.GetGpuUtilizationPercent();
        var config = edgeHardwareConfig.Value;

        var parts = new List<string>(4);
        if (energyWh is > 0 && config.EnergyReporting is not EnergyReportingMode.Off)
            parts.Add(FormatEnergy(energyWh.Value, config));
        if (gpuTemp is not null)
            parts.Add($"🌡 {gpuTemp:F0}°C");
        if (gpuUtil is not null)
            parts.Add($"⚙ {gpuUtil:F0}%");
        if (parts.Count == 0)
            return null;

        if (config.SolarContextEnabled && await GetSolarSnapshotAsync() is { } solar && DescribePowerSource(solar) is { Length: > 0 } source)
            parts.Add(source);

        return string.Join(" | ", parts);
    }

    private static string FormatEnergy(double energyWh, EdgeHardwareConfig config)
    {
        var energy = $"⚡ {energyWh:F2}Wh";
        if (config.EnergyReporting is not EnergyReportingMode.Verbose)
            return energy;

        // Fun comparisons.
        var comparisons = new List<string>();
        if (config.KettleBoilWh > 0)
            comparisons.Add($"~{energyWh / config.KettleBoilWh:F4} kettles");
        if (config.PhoneChargeWh > 0)
            comparisons.Add($"~{energyWh / config.PhoneChargeWh:F3} phone charges");
        if (config.LedBulbHourWh > 0)
            comparisons.Add($"~{energyWh / config.LedBulbHourWh:F3} LED-bulb-hrs");
        return comparisons.Count > 0 ? $"{energy} ({string.Join(" | ", comparisons)})" : energy;
    }

    private static string DescribePowerSource(InverterSnapshot solar)
    {
        if (solar.PhotovoltaicPower > Math.Abs(solar.LoadPower))
            return "☀\uFE0F solar-powered";
        if (solar.BatteryPower > 0)
            return "🔋 battery-powered";
        return solar.GridPower > 0 ? "🔌 grid" : string.Empty;
    }

    /// <inheritdoc/>
    public IEnumerable<string> FormatDebugLines(CommsAgentRunResult result)
    {
        if (result.GetEstimatedEnergyWh() is > 0 and var energy)
            yield return $"\u26A1 Energy: {energy:F4} Wh";
        if (result.GetGpuPowerDrawW() is > 0 and var power)
            yield return $"\u26A1 GPU: {power:F1} W";
        if (result.GetGpuTemperatureC() is { } temperature)
            yield return $"\U0001F321 GPU temp: {temperature:F0}\u00B0C";
        if (result.GetGpuUtilizationPercent() is { } utilization)
            yield return $"\u2699 GPU util: {utilization:F0}%";
    }

    private async Task<EdgeHardwareSnapshot?> GetLatestSnapshotAsync()
    {
        if (serviceProvider.GetService<IEdgeHardwareQueryService>() is not { } edgeHardwareQuerySvc)
            return null;
        var snapshots = await edgeHardwareQuerySvc.GetLatestSnapshots();
        return snapshots.FirstOrDefault();
    }

    //Solar context is decoration; an unreachable inverter must not fail the reply.
    private async Task<InverterSnapshot?> GetSolarSnapshotAsync()
    {
        if (serviceProvider.GetService<IFroniusQueryService>() is not { } froniusQuerySvc)
            return null;
        try
        {
            return await froniusQuerySvc.GetInverterSnapshot();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            return null;
        }
    }
}
