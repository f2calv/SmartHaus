using System.Net.Http.Json;

namespace CasCap.Services;

/// <summary>
/// HTTP client for the Shelly Cloud REST API.
/// </summary>
/// <remarks>
/// See <see href="https://shelly-api-docs.shelly.cloud/cloud-control-api/"/> for the API specification.
/// Targets the Shelly Plug S (Gen1) device via Cloud API.
/// </remarks>
public class ShellyCloudClientService(
    ILogger<ShellyCloudClientService> logger,
    IOptions<ShellyConfig> config,
    IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _client = httpClientFactory.CreateClient(nameof(ShellyCloudConnectionHealthCheck));
    private readonly string _authKey = config.Value.AuthKey;

    /// <summary>
    /// Retrieves the current device status from the Shelly Cloud API.
    /// </summary>
    /// <param name="deviceId">The Shelly device ID to query.</param>
    public async Task<ShellyDeviceStatusResponse?> GetDeviceStatus(string deviceId)
    {
        try
        {
            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("auth_key", _authKey),
                new KeyValuePair<string, string>("id", deviceId),
            ]);

            var response = await _client.PostAsync("device/status", content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ShellyDeviceStatusResponse>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{ClassName} failed to get device status for {DeviceId}", nameof(ShellyCloudClientService), deviceId);
        }
        return null;
    }

    /// <summary>
    /// Controls the relay state (on/off) via the Shelly Cloud API.
    /// </summary>
    /// <param name="deviceId">The Shelly device ID to control.</param>
    /// <param name="channel">The relay channel index.</param>
    /// <param name="turnOn">
    /// When <see langword="true"/>, turns the relay on; when <see langword="false"/>, turns it off.
    /// </param>
    public async Task<ShellyRelayControlResponse?> SetRelayState(string deviceId, int channel, bool turnOn)
    {
        try
        {
            var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("auth_key", _authKey),
                new KeyValuePair<string, string>("id", deviceId),
                new KeyValuePair<string, string>("channel", channel.ToString()),
                new KeyValuePair<string, string>("turn", turnOn ? "on" : "off"),
            ]);

            var response = await _client.PostAsync("device/relay/control", content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ShellyRelayControlResponse>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{ClassName} failed to set relay state to {DesiredState} for {DeviceId}",
                nameof(ShellyCloudClientService), turnOn ? "on" : "off", deviceId);
        }
        return null;
    }

}
