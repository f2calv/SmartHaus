namespace CasCap.Services;

/// <summary>
/// MCP wrapper for <see cref="IBuderusQueryService"/> that exposes heat pump operations as MCP tools.
/// </summary>
[McpServerToolType]
public partial class HeatPumpMcpQueryService(IBuderusQueryService buderusQuerySvc)
{
    /// <inheritdoc cref="IBuderusQueryService.GetSnapshot"/>
    [McpServerTool]
    [Description("Current state of the heat pump — temperatures, setpoints and operating mode.")]
    public Task<BuderusSnapshot> GetHeatPumpState() => buderusQuerySvc.GetSnapshot();

    /// <inheritdoc cref="IBuderusQueryService.SetDataPoint"/>
    [McpServerTool]
    [Description("Writes a value to a writeable heat pump datapoint.")]
    public Task<bool> SetHeatPumpDataPoint(
        [Description("Datapoint path, e.g. /dhwCircuits/dhw1/setTemperature.")]
        string datapointId,
        [Description("Numeric string (e.g. 55.0) or enum option (e.g. Always_On).")]
        string value,
        CancellationToken cancellationToken = default)
        => buderusQuerySvc.SetDataPoint(datapointId, value, cancellationToken);
}
