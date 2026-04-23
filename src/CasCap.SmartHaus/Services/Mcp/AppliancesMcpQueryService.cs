namespace CasCap.Services;

/// <summary>
/// MCP wrapper for <see cref="IMieleQueryService"/> that exposes home appliance operations as MCP tools.
/// </summary>
[McpServerToolType]
public partial class AppliancesMcpQueryService(IMieleQueryService mieleQuerySvc)
{
    /// <inheritdoc cref="IMieleQueryService.GetDevices"/>
    [McpServerTool]
    [Description("Lists all household appliances with full identification and current state.")]
    public Task<Dictionary<string, MieleDevice>?> GetAllAppliances(
        [Description("Language code, e.g. en, de.")]
        string language = "en")
        => mieleQuerySvc.GetDevices(language);

    /// <inheritdoc cref="IMieleQueryService.GetShortDevices"/>
    [McpServerTool]
    [Description("Compact summary of all household appliances — serial number, type and status only.")]
    public Task<MieleShortDevice[]?> GetAllAppliancesSummary(
        [Description("Language code, e.g. en, de.")]
        string language = "en")
        => mieleQuerySvc.GetShortDevices(language);

    /// <inheritdoc cref="IMieleQueryService.GetDevice"/>
    [McpServerTool]
    [Description("Full detail for one appliance — identification and current state.")]
    public Task<MieleDevice?> GetAppliance(
        [Description("Device serial number.")]
        string deviceId,
        [Description("Language code, e.g. en, de.")]
        string language = "en")
        => mieleQuerySvc.GetDevice(deviceId, language);

    /// <inheritdoc cref="IMieleQueryService.GetIdent"/>
    [McpServerTool]
    [Description("Identification info for one appliance — model name, type and serial number.")]
    public Task<MieleIdent?> GetApplianceIdentification(
        [Description("Device serial number.")]
        string deviceId,
        [Description("Language code, e.g. en, de.")]
        string language = "en")
        => mieleQuerySvc.GetIdent(deviceId, language);

    /// <inheritdoc cref="IMieleQueryService.GetState"/>
    [McpServerTool]
    [Description("Operational state of one appliance — status, program phase, remaining time and temperatures.")]
    public Task<MieleState?> GetApplianceState(
        [Description("Device serial number.")]
        string deviceId,
        [Description("Language code, e.g. en, de.")]
        string language = "en")
        => mieleQuerySvc.GetState(deviceId, language);

    /// <inheritdoc cref="IMieleQueryService.GetActions"/>
    [McpServerTool]
    [Description("Currently available actions for one appliance (e.g. start, stop, powerOn, powerOff).")]
    public Task<MieleActions?> GetApplianceActions(
        [Description("Device serial number.")]
        string deviceId)
        => mieleQuerySvc.GetActions(deviceId);

    /// <inheritdoc cref="IMieleQueryService.PutAction"/>
    [McpServerTool]
    [Description("Executes an action on one appliance — powerOn, powerOff, start or stop.")]
    public Task<bool> ExecuteApplianceAction(
        [Description("Device serial number.")]
        string deviceId,
        [Description("Action payload object.")]
        object action)
        => mieleQuerySvc.PutAction(deviceId, action);

    /// <inheritdoc cref="IMieleQueryService.GetPrograms"/>
    [McpServerTool]
    [Description("Lists the selectable programs for one appliance.")]
    public Task<MieleProgram[]?> GetAppliancePrograms(
        [Description("Device serial number.")]
        string deviceId,
        [Description("Language code, e.g. en, de.")]
        string language = "en")
        => mieleQuerySvc.GetPrograms(deviceId, language);

    /// <inheritdoc cref="IMieleQueryService.PutProgram"/>
    [McpServerTool]
    [Description("Selects and starts a program on one appliance. Requires MobileStart or MobileControl mode.")]
    public Task<bool> StartApplianceProgram(
        [Description("Device serial number.")]
        string deviceId,
        [Description("Program selection payload.")]
        object programRequest)
        => mieleQuerySvc.PutProgram(deviceId, programRequest);
}
