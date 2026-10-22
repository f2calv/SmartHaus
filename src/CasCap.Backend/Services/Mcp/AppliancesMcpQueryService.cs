namespace CasCap.Services;

/// <summary>
/// MCP wrapper for <see cref="IMieleQueryService"/> that exposes home appliance operations as MCP tools.
/// </summary>
[McpServerToolType]
public sealed partial class AppliancesMcpQueryService(IMieleQueryService mieleQuerySvc)
{
    /// <inheritdoc cref="IMieleQueryService.GetDevices"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists all household appliances with full identification and current state.")]
    public Task<Dictionary<string, MieleDevice>?> GetAllAppliances(
        [Description("Language code, e.g. en, de.")]
        string language = "en",
        CancellationToken cancellationToken = default)
        => mieleQuerySvc.GetDevices(language, cancellationToken);

    /// <inheritdoc cref="IMieleQueryService.GetShortDevices"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Compact summary of all household appliances — serial number, type and status only.")]
    public Task<MieleShortDevice[]?> GetAllAppliancesSummary(
        [Description("Language code, e.g. en, de.")]
        string language = "en",
        CancellationToken cancellationToken = default)
        => mieleQuerySvc.GetShortDevices(language, cancellationToken);

    /// <inheritdoc cref="IMieleQueryService.GetDevice"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Full detail for one appliance — identification and current state.")]
    public Task<MieleDevice?> GetAppliance(
        [Description("Device serial number.")]
        string deviceId,
        [Description("Language code, e.g. en, de.")]
        string language = "en",
        CancellationToken cancellationToken = default)
        => mieleQuerySvc.GetDevice(deviceId, language, cancellationToken);

    /// <inheritdoc cref="IMieleQueryService.GetIdent"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Identification info for one appliance — model name, type and serial number.")]
    public Task<MieleIdent?> GetApplianceIdentification(
        [Description("Device serial number.")]
        string deviceId,
        [Description("Language code, e.g. en, de.")]
        string language = "en",
        CancellationToken cancellationToken = default)
        => mieleQuerySvc.GetIdent(deviceId, language, cancellationToken);

    /// <inheritdoc cref="IMieleQueryService.GetState"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Operational state of one appliance — status, program phase, remaining time and temperatures.")]
    public Task<MieleState?> GetApplianceState(
        [Description("Device serial number.")]
        string deviceId,
        [Description("Language code, e.g. en, de.")]
        string language = "en",
        CancellationToken cancellationToken = default)
        => mieleQuerySvc.GetState(deviceId, language, cancellationToken);

    /// <inheritdoc cref="IMieleQueryService.GetActions"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Currently available actions for one appliance (e.g. start, stop, powerOn, powerOff).")]
    public Task<MieleActions?> GetApplianceActions(
        [Description("Device serial number.")]
        string deviceId,
        CancellationToken cancellationToken = default)
        => mieleQuerySvc.GetActions(deviceId, cancellationToken);

    /// <inheritdoc cref="IMieleQueryService.PutAction"/>
    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Executes an action on one appliance — powerOn, powerOff, start or stop.")]
    public async Task<bool> ExecuteApplianceAction(
        [Description("Device serial number.")]
        string deviceId,
        [Description("Typed appliance action; supply at least one supported field. runOnTime is not supported.")]
        MieleActionRequest action,
        CancellationToken cancellationToken = default)
        => await mieleQuerySvc.PutAction(deviceId, action, cancellationToken).ConfigureAwait(false) == MieleCommandResult.Accepted;

    /// <inheritdoc cref="IMieleQueryService.GetPrograms"/>
    [McpServerTool(ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Lists the selectable programs for one appliance.")]
    public Task<MieleProgram[]?> GetAppliancePrograms(
        [Description("Device serial number.")]
        string deviceId,
        [Description("Language code, e.g. en, de.")]
        string language = "en",
        CancellationToken cancellationToken = default)
        => mieleQuerySvc.GetPrograms(deviceId, language, cancellationToken);

    /// <inheritdoc cref="IMieleQueryService.PutProgram"/>
    [McpServerTool(ReadOnly = false, Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Selects and starts a program on one appliance. Requires MobileStart or MobileControl mode.")]
    public async Task<bool> StartApplianceProgram(
        [Description("Device serial number.")]
        string deviceId,
        [Description("Program selection with required programId, optional duration [hours, minutes] and optional temperature.")]
        MieleProgramRequest programRequest,
        CancellationToken cancellationToken = default)
        => await mieleQuerySvc.PutProgram(deviceId, programRequest, cancellationToken).ConfigureAwait(false) == MieleCommandResult.Accepted;
}
