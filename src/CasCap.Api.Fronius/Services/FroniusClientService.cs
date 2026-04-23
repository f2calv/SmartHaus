using CasCap.Common.Services;

namespace CasCap.Services;

/// <summary>
/// HTTP client for the Fronius Solar API v1.
/// </summary>
/// <remarks>
/// See <see href="https://www.fronius.com/~/downloads/Solar%20Energy/Operating%20Instructions/42,0410,2012.pdf"/> for the full API specification.
/// Our inverter model is a Fronius Symo Gen24.
/// </remarks>
public class FroniusClientService : HttpClientBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FroniusClientService"/> class.
    /// </summary>
    public FroniusClientService(ILogger<FroniusClientService> logger,
        IOptions<FroniusConfig> config,
        IHttpClientFactory httpClientFactory,
        IHostEnvironment env)
    {
        _logger = logger;
        Client = httpClientFactory.CreateClient(nameof(FroniusSymoConnectionHealthCheck));
        if (env.IsDevelopment() && config.Value.JsonDebugEnabled && !string.IsNullOrWhiteSpace(config.Value.JsonDebugPath))
            JsonDebugPath = config.Value.JsonDebugPath;
    }

    #region Inverter

    /// <summary>
    /// Retrieves real-time power flow data from <c>GetPowerFlowRealtimeData.fcgi</c>.
    /// </summary>
    public async Task<ApiWrapper<PowerFlowRealtimeData>?> GetPowerFlowRealtimeData()
    {
        var requestUri = "solar_api/v1/GetPowerFlowRealtimeData.fcgi";
        try
        {
            var tpl = await base.GetAsync<ApiWrapper<PowerFlowRealtimeData>, string>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get power flow realtime data", nameof(FroniusClientService));
        }
        return null;
    }

    /// <summary>
    /// Retrieves inverter real-time data from <c>GetInverterRealtimeData.cgi</c>.
    /// </summary>
    /// <param name="dataCollection">The data collection to retrieve (CommonInverterData, 3PInverterData, or CumulationInverterData).</param>
    public async Task<ApiWrapper<CommonInverterData>?> GetInverterRealtimeData(string dataCollection = "CommonInverterData")
    {
        var requestUri = $"solar_api/v1/GetInverterRealtimeData.cgi?Datacollection={dataCollection}";
        try
        {
            var tpl = await base.GetAsync<ApiWrapper<CommonInverterData>, string>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get inverter realtime data", nameof(FroniusClientService));
        }
        return null;
    }

    /// <summary>
    /// Retrieves inverter device information from <c>GetInverterInfo.cgi</c>.
    /// </summary>
    public async Task<ApiWrapper<Dictionary<string, InverterInfoEntry>>?> GetInverterInfo()
    {
        var requestUri = "solar_api/v1/GetInverterInfo.cgi";
        try
        {
            var tpl = await base.GetAsync<ApiWrapper<Dictionary<string, InverterInfoEntry>>, string>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get inverter info", nameof(FroniusClientService));
        }
        return null;
    }

    #endregion

    #region External devices

    /// <summary>
    /// Retrieves active device information from <c>GetActiveDeviceInfo.cgi</c>.
    /// </summary>
    public async Task<ApiWrapper<ActiveDeviceInfoData>?> GetActiveDeviceInfo()
    {
        var requestUri = "solar_api/v1/GetActiveDeviceInfo.cgi";
        try
        {
            var tpl = await base.GetAsync<ApiWrapper<ActiveDeviceInfoData>, string>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get active device info", nameof(FroniusClientService));
        }
        return null;
    }

    /// <summary>
    /// Retrieves meter real-time data from <c>GetMeterRealtimeData.cgi</c>.
    /// </summary>
    /// <param name="scope">The scope of the query ("Device" or "System"). Default is "System".</param>
    /// <param name="deviceId">Optional device ID when scope is "Device".</param>
    public async Task<ApiWrapper<Dictionary<string, MeterRealtimeData>>?> GetMeterRealtimeData(string scope = "System", int? deviceId = null)
    {
        var requestUri = $"solar_api/v1/GetMeterRealtimeData.cgi?Scope={scope}";
        if (deviceId.HasValue)
            requestUri += $"&DeviceId={deviceId.Value}";
        try
        {
            var tpl = await base.GetAsync<ApiWrapper<Dictionary<string, MeterRealtimeData>>, string>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get meter realtime data", nameof(FroniusClientService));
        }
        return null;
    }

    /// <summary>
    /// Retrieves storage real-time data from <c>GetStorageRealtimeData.cgi</c>.
    /// </summary>
    /// <param name="scope">The scope of the query ("Device" or "System"). Default is "System".</param>
    /// <param name="deviceId">Optional device ID when scope is "Device".</param>
    public async Task<ApiWrapper<Dictionary<string, StorageRealtimeData>>?> GetStorageRealtimeData(string scope = "System", int? deviceId = null)
    {
        var requestUri = $"solar_api/v1/GetStorageRealtimeData.cgi?Scope={scope}";
        if (deviceId.HasValue)
            requestUri += $"&DeviceId={deviceId.Value}";
        try
        {
            var tpl = await base.GetAsync<ApiWrapper<Dictionary<string, StorageRealtimeData>>, string>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get storage realtime data", nameof(FroniusClientService));
        }
        return null;
    }

    #endregion
}
