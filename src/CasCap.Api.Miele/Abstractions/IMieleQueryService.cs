namespace CasCap.Abstractions;

/// <summary>
/// Defines the public query and action operations exposed by the Miele appliance service.
/// </summary>
public interface IMieleQueryService
{
    /// <summary>
    /// Returns all Miele appliances linked to the user account with their identification and current state.
    /// </summary>
    /// <param name="language">Language code for localized values (e.g. 'en', 'de'). Default 'en'.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<Dictionary<string, MieleDevice>?> GetDevices(string language = "en", CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a compact summary of all Miele appliances with serial number, state and type.
    /// </summary>
    /// <param name="language">Language code for localized values (e.g. 'en', 'de'). Default 'en'.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<MieleShortDevice[]?> GetShortDevices(string language = "en", CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns full identification and state for a single Miele appliance.
    /// </summary>
    /// <param name="deviceId">The device ID (serial number) to query.</param>
    /// <param name="language">Language code for localized values (e.g. 'en', 'de'). Default 'en'.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<MieleDevice?> GetDevice(string deviceId, string language = "en", CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns identification information for a single Miele appliance.
    /// </summary>
    /// <param name="deviceId">The device ID (serial number) to query.</param>
    /// <param name="language">Language code for localized values (e.g. 'en', 'de'). Default 'en'.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<MieleIdent?> GetIdent(string deviceId, string language = "en", CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current operational state of a single Miele appliance.
    /// </summary>
    /// <param name="deviceId">The device ID (serial number) to query.</param>
    /// <param name="language">Language code for localized values (e.g. 'en', 'de'). Default 'en'.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<MieleState?> GetState(string deviceId, string language = "en", CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the currently available actions for a Miele appliance.
    /// </summary>
    /// <param name="deviceId">The device ID (serial number) to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<MieleActions?> GetActions(string deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invokes an action on a Miele appliance (e.g. powerOn, powerOff, start, stop).
    /// </summary>
    /// <param name="deviceId">The device ID (serial number) to act on.</param>
    /// <param name="action">The typed action payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<MieleCommandResult> PutAction(string deviceId, MieleActionRequest action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the available programs for a Miele appliance.
    /// </summary>
    /// <param name="deviceId">The device ID (serial number) to query.</param>
    /// <param name="language">Language code for localized values (e.g. 'en', 'de'). Default 'en'.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<MieleProgram[]?> GetPrograms(string deviceId, string language = "en", CancellationToken cancellationToken = default);

    /// <summary>
    /// Selects and starts a program on a Miele appliance. The device must be set to MobileStart or MobileControl.
    /// </summary>
    /// <param name="deviceId">The device ID (serial number) to act on.</param>
    /// <param name="programRequest">The program selection payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<MieleCommandResult> PutProgram(string deviceId, MieleProgramRequest programRequest, CancellationToken cancellationToken = default);
}
