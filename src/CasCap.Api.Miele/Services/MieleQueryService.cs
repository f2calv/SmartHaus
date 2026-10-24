namespace CasCap.Services;

/// <summary>
/// Provides access to Miele appliance data by querying the Miele 3rd Party API.
/// </summary>
/// <remarks>
/// Key functions are also made accessible via <see cref="CasCap.Controllers.MieleController"/>.
/// </remarks>
public sealed class MieleQueryService(
    ILogger<MieleQueryService> logger,
    MieleClientService clientSvc) : IMieleQueryService
{
    /// <inheritdoc cref="MieleClientService.GetDevices"/>
    public async Task<Dictionary<string, MieleDevice>?> GetDevices(
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("{ClassName} retrieving devices", nameof(MieleQueryService));
        return await clientSvc.GetDevices(language, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="MieleClientService.GetShortDevices"/>
    public Task<MieleShortDevice[]?> GetShortDevices(
        string language = "en",
        CancellationToken cancellationToken = default)
        => clientSvc.GetShortDevices(language, cancellationToken);

    /// <inheritdoc cref="MieleClientService.GetDevice"/>
    public Task<MieleDevice?> GetDevice(
        string deviceId,
        string language = "en",
        CancellationToken cancellationToken = default)
        => clientSvc.GetDevice(deviceId, language, cancellationToken);

    /// <inheritdoc cref="MieleClientService.GetIdent"/>
    public Task<MieleIdent?> GetIdent(
        string deviceId,
        string language = "en",
        CancellationToken cancellationToken = default)
        => clientSvc.GetIdent(deviceId, language, cancellationToken);

    /// <inheritdoc cref="MieleClientService.GetState"/>
    public Task<MieleState?> GetState(
        string deviceId,
        string language = "en",
        CancellationToken cancellationToken = default)
        => clientSvc.GetState(deviceId, language, cancellationToken);

    /// <inheritdoc cref="MieleClientService.GetActions"/>
    public Task<MieleActions?> GetActions(
        string deviceId,
        CancellationToken cancellationToken = default)
        => clientSvc.GetActions(deviceId, cancellationToken);

    /// <inheritdoc cref="MieleClientService.PutAction"/>
    public Task<MieleCommandResult> PutAction(
        string deviceId,
        MieleActionRequest action,
        CancellationToken cancellationToken = default)
        => clientSvc.PutAction(deviceId, action, cancellationToken);

    /// <inheritdoc cref="MieleClientService.GetPrograms"/>
    public Task<MieleProgram[]?> GetPrograms(
        string deviceId,
        string language = "en",
        CancellationToken cancellationToken = default)
        => clientSvc.GetPrograms(deviceId, language, cancellationToken);

    /// <inheritdoc cref="MieleClientService.PutProgram"/>
    public Task<MieleCommandResult> PutProgram(
        string deviceId,
        MieleProgramRequest programRequest,
        CancellationToken cancellationToken = default)
        => clientSvc.PutProgram(deviceId, programRequest, cancellationToken);
}
