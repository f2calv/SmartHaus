namespace CasCap.Services;

/// <summary>
/// Provides access to Miele appliance data by querying the Miele 3rd Party API.
/// </summary>
/// <remarks>
/// Key functions are also made accessible via <see cref="CasCap.Controllers.MieleController"/>.
/// </remarks>
public class MieleQueryService(
    ILogger<MieleQueryService> logger,
    MieleClientService clientSvc) : IMieleQueryService
{
    /// <inheritdoc cref="MieleClientService.GetDevices"/>
    public async Task<Dictionary<string, MieleDevice>?> GetDevices(
        string language = "en")
    {
        logger.LogDebug("{ClassName} retrieving devices", nameof(MieleQueryService));
        return await clientSvc.GetDevices(language);
    }

    /// <inheritdoc cref="MieleClientService.GetShortDevices"/>
    public async Task<MieleShortDevice[]?> GetShortDevices(
        string language = "en")
        => await clientSvc.GetShortDevices(language);

    /// <inheritdoc cref="MieleClientService.GetDevice"/>
    public async Task<MieleDevice?> GetDevice(
        string deviceId,
        string language = "en")
        => await clientSvc.GetDevice(deviceId, language);

    /// <inheritdoc cref="MieleClientService.GetIdent"/>
    public async Task<MieleIdent?> GetIdent(
        string deviceId,
        string language = "en")
        => await clientSvc.GetIdent(deviceId, language);

    /// <inheritdoc cref="MieleClientService.GetState"/>
    public async Task<MieleState?> GetState(
        string deviceId,
        string language = "en")
        => await clientSvc.GetState(deviceId, language);

    /// <inheritdoc cref="MieleClientService.GetActions"/>
    public async Task<MieleActions?> GetActions(
        string deviceId)
        => await clientSvc.GetActions(deviceId);

    /// <inheritdoc cref="MieleClientService.PutAction"/>
    public async Task<bool> PutAction(
        string deviceId,
        object action)
        => await clientSvc.PutAction(deviceId, action);

    /// <inheritdoc cref="MieleClientService.GetPrograms"/>
    public async Task<MieleProgram[]?> GetPrograms(
        string deviceId,
        string language = "en")
        => await clientSvc.GetPrograms(deviceId, language);

    /// <inheritdoc cref="MieleClientService.PutProgram"/>
    public async Task<bool> PutProgram(
        string deviceId,
        object programRequest)
        => await clientSvc.PutProgram(deviceId, programRequest);
}
