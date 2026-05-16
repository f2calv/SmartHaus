namespace CasCap.Services;

/// <summary>
/// MCP wrapper for <see cref="IShellyQueryService"/> that exposes smart plug and relay operations as MCP tools.
/// </summary>
[McpServerToolType]
public partial class SmartPlugMcpQueryService(IShellyQueryService shellyQuerySvc)
{
    /// <inheritdoc cref="IShellyQueryService.GetSnapshots"/>
    [McpServerTool]
    [Description("Smart plug snapshots for all devices — current power consumption, relay on/off state, temperature and overpower status.")]
    public Task<List<ShellySnapshot>> GetSmartPlugSnapshots() => shellyQuerySvc.GetSnapshots();

    /// <inheritdoc cref="IShellyQueryService.GetDeviceStatus"/>
    [McpServerTool]
    [Description("Full device status — relay state, power meter, temperature, online status.")]
    public Task<ShellyDeviceStatusResponse?> GetSmartPlugStatus(
        [Description("Device ID to query.")]
        string deviceId) => shellyQuerySvc.GetDeviceStatus(deviceId);

    /// <inheritdoc cref="IShellyQueryService.SetRelayState"/>
    [McpServerTool]
    [Description("Turn a smart plug relay on or off.")]
    [RequiresApproval(Reason = "Controls physical appliance power state.")]
    public Task<ShellyRelayControlResponse?> SetSmartPlugPower(
        [Description("Device ID to control.")]
        string deviceId,
        [Description("true = turn on, false = turn off")]
        bool on) => shellyQuerySvc.SetRelayState(deviceId, on);
}
