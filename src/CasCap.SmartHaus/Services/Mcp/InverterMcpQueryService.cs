namespace CasCap.Services;

/// <summary>
/// MCP wrapper for <see cref="IFroniusQueryService"/> that exposes solar inverter operations as MCP tools.
/// </summary>
[McpServerToolType]
public partial class InverterMcpQueryService(IFroniusQueryService froniusQuerySvc)
{
    /// <inheritdoc cref="IFroniusQueryService.GetPowerFlowRealtimeData"/>
    [McpServerTool]
    [Description("Real-time power flow — solar production, grid import/export, battery charge/discharge and self-consumption.")]
    public Task<PowerFlowRealtimeData?> GetInverterPowerFlow() => froniusQuerySvc.GetPowerFlowRealtimeData();

    /// <inheritdoc cref="IFroniusQueryService.GetInverterRealtimeData"/>
    [McpServerTool]
    [Description("Real-time electrical readings — AC/DC voltage, current, power and energy counters from the inverter.")]
    public Task<CommonInverterData?> GetInverterElectricalReadings(
        [Description("Values: CommonInverterData (AC/DC measurements), 3PInverterData (three-phase), CumulationInverterData (cumulative totals).")]
        string dataCollection = "CommonInverterData")
        => froniusQuerySvc.GetInverterRealtimeData(dataCollection);

    /// <inheritdoc cref="IFroniusQueryService.GetInverterInfo"/>
    [McpServerTool]
    [Description("Device info for all inverters — name, serial number and operational state.")]
    public Task<Dictionary<string, InverterInfoEntry>?> GetInverterInfo() => froniusQuerySvc.GetInverterInfo();

    /// <inheritdoc cref="IFroniusQueryService.GetActiveDeviceInfo"/>
    [McpServerTool]
    [Description("Lists all devices connected to the inverter system — inverters, meters, batteries and Ohmpilot.")]
    public Task<ActiveDeviceInfoData?> GetInverterConnectedDevices() => froniusQuerySvc.GetActiveDeviceInfo();

    /// <inheritdoc cref="IFroniusQueryService.GetMeterRealtimeData"/>
    [McpServerTool]
    [Description("Real-time smart meter readings — power, voltage, current and energy totals.")]
    public Task<Dictionary<string, MeterRealtimeData>?> GetInverterMeterReadings(
        [Description("Values: System (aggregated totals), Device (single meter — requires deviceId).")]
        string scope = "System",
        [Description("Meter device ID. Required when scope is Device.")]
        int? deviceId = null)
        => froniusQuerySvc.GetMeterRealtimeData(scope, deviceId);

    /// <inheritdoc cref="IFroniusQueryService.GetStorageRealtimeData"/>
    [McpServerTool]
    [Description("Real-time battery status — state of charge, capacity, temperature and charge/discharge rate.")]
    public Task<Dictionary<string, StorageRealtimeData>?> GetInverterBatteryStatus(
        [Description("Values: System (aggregated totals), Device (single battery — requires deviceId).")]
        string scope = "System",
        [Description("Battery device ID. Required when scope is Device.")]
        int? deviceId = null)
        => froniusQuerySvc.GetStorageRealtimeData(scope, deviceId);

    /// <inheritdoc cref="IFroniusQueryService.GetInverterSnapshot"/>
    [McpServerTool]
    [Description("High-level snapshot — home consumption, grid feed/draw, solar production and battery percentage in one call.")]
    public Task<InverterSnapshot> GetInverterSnapshot() => froniusQuerySvc.GetInverterSnapshot();
}
