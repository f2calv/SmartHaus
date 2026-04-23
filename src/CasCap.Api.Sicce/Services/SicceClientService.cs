using CasCap.Common.Services;

namespace CasCap.Services;

/// <summary>
/// https://documenter.getpostman.com/view/24541810/2s8Ysp1ahG#bf99c565-3182-4d18-9a90-7743cb043b37
/// production: https://sicce.thingscloud.it
/// test: https://sicce-test.thingscloud.it
/// </summary>
public class SicceClientService : HttpClientBase
{
    //private readonly ILogger _logger;
    private readonly SicceConfig _appConfig;
    //private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>Initializes a new instance of the <see cref="SicceClientService"/> class.</summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="config">Sicce configuration.</param>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    public SicceClientService(ILogger<SicceClientService> logger,
        IOptions<SicceConfig> config,
        IHttpClientFactory httpClientFactory
        )
    {
        _logger = logger;
        _appConfig = config.Value;
        Client = httpClientFactory.CreateClient(nameof(SicceConnectionHealthCheck));
    }

    /// <summary>
    /// Gets the device information from the Sicce API.
    /// </summary>
    /// <returns>
    /// Device information, or null if the request fails.
    /// </returns>
    public async Task<DeviceInfo?> GetDeviceInfo()
    {
        var requestUri = $"/api/v3?device_token={_appConfig.DeviceToken}";
        (ResponseWrapper<DeviceInfo>? result, string? error) tpl = default;
        try
        {
            tpl = await base.GetAsync<ResponseWrapper<DeviceInfo>, string>(requestUri);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"{nameof(GetDeviceInfo)} failure");
        }
        return tpl.result?.Data;
    }

    /// <summary>
    /// Updates the device status.
    /// </summary>
    /// <param name="req">
    /// Update status request.
    /// </param>
    /// <returns>
    /// True if the update was successful; otherwise, false.
    /// </returns>
    public Task<bool> UpdateStatus(UpdateStatus req) => _UpdateStatus(req);

    /// <summary>
    /// Sets the power switch state.
    /// </summary>
    /// <param name="on">
    /// True to turn on; false to turn off.
    /// </param>
    /// <returns>
    /// True if the update was successful; otherwise, false.
    /// </returns>
    public Task<bool> SetPowerSwitch(bool on) => _UpdateStatus(new Power { PowerSwitch = on });

    private async Task<bool> _UpdateStatus(object req)
    {
        var requestUri = $"/api/v3?update_status={_appConfig.DeviceToken}";
        (Response? result, string? error) tpl = default;
        try
        {
            tpl = await base.PostJsonAsync<Response, string>(requestUri, req);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{ClassName} failure to process request {@request}",
                nameof(_UpdateStatus), req);
        }
        return tpl.result is not null && tpl.result.Result;
    }
}
