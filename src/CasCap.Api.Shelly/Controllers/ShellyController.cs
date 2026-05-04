namespace CasCap.Controllers;

/// <summary>
/// REST API controller for Shelly smart plug data queries and relay control.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class ShellyController(IShellyQueryService shellyQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="ShellyQueryService.GetSnapshots"/>
    [HttpGet]
    [ProducesResponseType<List<ShellySnapshot>>(StatusCodes.Status200OK)]
    public async Task<Ok<List<ShellySnapshot>>> GetSnapshots()
        => TypedResults.Ok(await shellyQuerySvc.GetSnapshots());

    /// <inheritdoc cref="ShellyQueryService.GetReadings"/>
    [HttpGet("readings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Ok<IAsyncEnumerable<ShellyEvent>> GetReadings(int limit = 100)
        => TypedResults.Ok(shellyQuerySvc.GetReadings(limit));

    /// <inheritdoc cref="ShellyQueryService.GetDeviceStatus"/>
    [HttpGet("status/{deviceId}")]
    [ProducesResponseType<ShellyDeviceStatusResponse>(StatusCodes.Status200OK)]
    public async Task<Ok<ShellyDeviceStatusResponse>> GetDeviceStatus(string deviceId)
        => TypedResults.Ok(await shellyQuerySvc.GetDeviceStatus(deviceId));

    /// <summary>
    /// Turns the smart plug relay on or off.
    /// </summary>
    /// <param name="deviceId">The Shelly device ID to control.</param>
    /// <param name="on">When <see langword="true"/>, turns the relay on; when <see langword="false"/>, turns it off.</param>
    [HttpPost("relay/{deviceId}")]
    [ProducesResponseType<ShellyRelayControlResponse>(StatusCodes.Status200OK)]
    public async Task<Ok<ShellyRelayControlResponse>> SetRelayState(string deviceId, [FromQuery] bool on)
        => TypedResults.Ok(await shellyQuerySvc.SetRelayState(deviceId, on));
}
