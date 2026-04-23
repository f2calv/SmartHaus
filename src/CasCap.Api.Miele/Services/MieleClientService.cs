using CasCap.Common.Services;

namespace CasCap.Services;

/// <summary>
/// HTTP client for the Miele 3rd Party API v1.
/// </summary>
/// <remarks>
/// See <see href="https://developer.miele.com"/> for the full API specification.
/// Uses OAuth 2.0 Bearer token authentication.
/// </remarks>
public class MieleClientService : HttpClientBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MieleClientService"/> class.
    /// </summary>
    public MieleClientService(ILogger<MieleClientService> logger,
        IOptions<MieleConfig> config,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        Client = httpClientFactory.CreateClient(nameof(MieleConnectionHealthCheck));
    }

    #region Ident + State

    /// <summary>
    /// Returns all information about all appliances linked to the user account.
    /// </summary>
    /// <param name="language">Optional language code (e.g. "en", "de").</param>
    public async Task<Dictionary<string, MieleDevice>?> GetDevices(string language = "en")
    {
        var requestUri = $"devices?language={language}";
        try
        {
            var tpl = await base.GetAsync<Dictionary<string, MieleDevice>, object>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get devices", nameof(MieleClientService));
        }
        return null;
    }

    /// <summary>
    /// Returns a reduced information set about all appliances for lightweight clients.
    /// </summary>
    /// <param name="language">Optional language code (e.g. "en", "de").</param>
    public async Task<MieleShortDevice[]?> GetShortDevices(string language = "en")
    {
        var requestUri = $"short/devices?language={language}";
        try
        {
            var tpl = await base.GetAsync<MieleShortDevice[], object>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get short devices", nameof(MieleClientService));
        }
        return null;
    }

    /// <summary>
    /// Returns all information about a single device.
    /// </summary>
    /// <param name="deviceId">The device ID to query.</param>
    /// <param name="language">Optional language code (e.g. "en", "de").</param>
    public async Task<MieleDevice?> GetDevice(string deviceId, string language = "en")
    {
        var requestUri = $"devices/{deviceId}?language={language}";
        try
        {
            var tpl = await base.GetAsync<MieleDevice, object>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get device '{DeviceId}'", nameof(MieleClientService), deviceId);
        }
        return null;
    }

    /// <summary>
    /// Returns the identification information of a single device.
    /// </summary>
    /// <param name="deviceId">The device ID to query.</param>
    /// <param name="language">Optional language code (e.g. "en", "de").</param>
    public async Task<MieleIdent?> GetIdent(string deviceId, string language = "en")
    {
        var requestUri = $"devices/{deviceId}/ident?language={language}";
        try
        {
            var tpl = await base.GetAsync<MieleIdent, object>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get ident for device '{DeviceId}'", nameof(MieleClientService), deviceId);
        }
        return null;
    }

    /// <summary>
    /// Returns the state information of a single device.
    /// </summary>
    /// <param name="deviceId">The device ID to query.</param>
    /// <param name="language">Optional language code (e.g. "en", "de").</param>
    public async Task<MieleState?> GetState(string deviceId, string language = "en")
    {
        var requestUri = $"devices/{deviceId}/state?language={language}";
        try
        {
            var tpl = await base.GetAsync<MieleState, object>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get state for device '{DeviceId}'", nameof(MieleClientService), deviceId);
        }
        return null;
    }

    #endregion

    #region Actions

    /// <summary>
    /// Returns the current available actions for a device.
    /// </summary>
    /// <param name="deviceId">The device ID to query.</param>
    public async Task<MieleActions?> GetActions(string deviceId)
    {
        var requestUri = $"devices/{deviceId}/actions";
        try
        {
            var tpl = await base.GetAsync<MieleActions, object>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get actions for device '{DeviceId}'", nameof(MieleClientService), deviceId);
        }
        return null;
    }

    /// <summary>
    /// Invokes an action on a single device.
    /// </summary>
    /// <param name="deviceId">The device ID to act on.</param>
    /// <param name="action">The action payload (e.g. processAction, powerOn, light).</param>
    /// <returns><see langword="true"/> if the action was accepted (HTTP 204).</returns>
    public async Task<bool> PutAction(string deviceId, object action)
    {
        var requestUri = $"devices/{deviceId}/actions";
        try
        {
            var response = await Client.PutAsJsonAsync(requestUri, action);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("{ClassName} action sent to device '{DeviceId}'", nameof(MieleClientService), deviceId);
                return true;
            }
            _logger.LogWarning("{ClassName} action rejected for device '{DeviceId}': {StatusCode}",
                nameof(MieleClientService), deviceId, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to put action for device '{DeviceId}'", nameof(MieleClientService), deviceId);
        }
        return false;
    }

    #endregion

    #region Programs

    /// <summary>
    /// Returns the current available programs for a device.
    /// </summary>
    /// <param name="deviceId">The device ID to query.</param>
    /// <param name="language">Optional language code (e.g. "en", "de").</param>
    public async Task<MieleProgram[]?> GetPrograms(string deviceId, string language = "en")
    {
        var requestUri = $"devices/{deviceId}/programs?language={language}";
        try
        {
            var tpl = await base.GetAsync<MieleProgram[], object>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get programs for device '{DeviceId}'", nameof(MieleClientService), deviceId);
        }
        return null;
    }

    /// <summary>
    /// Selects a program and starts the appliance. The device must be set to MobileStart or MobileControl.
    /// </summary>
    /// <param name="deviceId">The device ID to act on.</param>
    /// <param name="programRequest">The program selection payload.</param>
    /// <returns><see langword="true"/> if the program was accepted (HTTP 204).</returns>
    public async Task<bool> PutProgram(string deviceId, object programRequest)
    {
        var requestUri = $"devices/{deviceId}/programs";
        try
        {
            var response = await Client.PutAsJsonAsync(requestUri, programRequest);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("{ClassName} program started on device '{DeviceId}'", nameof(MieleClientService), deviceId);
                return true;
            }
            _logger.LogWarning("{ClassName} program rejected for device '{DeviceId}': {StatusCode}",
                nameof(MieleClientService), deviceId, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to put program for device '{DeviceId}'", nameof(MieleClientService), deviceId);
        }
        return false;
    }

    #endregion
}
