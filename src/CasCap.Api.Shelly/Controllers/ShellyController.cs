using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CasCap.Controllers;

/// <summary>
/// REST API controller for Shelly smart plug data queries and relay control.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class ShellyController(IShellyQueryService shellyQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="ShellyQueryService.GetSnapshots"/>
    [HttpGet]
    public async Task<Ok<List<ShellySnapshot>>> GetSnapshots()
        => TypedResults.Ok(await shellyQuerySvc.GetSnapshots());

    /// <inheritdoc cref="ShellyQueryService.GetReadings"/>
    [HttpGet("readings")]
    public Ok<IAsyncEnumerable<ShellyEvent>> GetReadings(CancellationToken cancellationToken, int limit = 100)
        => TypedResults.Ok(shellyQuerySvc.GetReadings(limit, cancellationToken));

    /// <inheritdoc cref="ShellyQueryService.GetDeviceStatus"/>
    [HttpGet("status/{deviceId}")]
    public async Task<Ok<ShellyDeviceStatusResponse>> GetDeviceStatus(string deviceId)
        => TypedResults.Ok(await shellyQuerySvc.GetDeviceStatus(deviceId));

    /// <summary>
    /// Turns the smart plug relay on or off.
    /// </summary>
    /// <param name="deviceId">The Shelly device ID to control.</param>
    /// <param name="on">When <see langword="true"/>, turns the relay on; when <see langword="false"/>, turns it off.</param>
    [HttpPost("relay/{deviceId}")]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<Ok<ShellyRelayControlResponse>> SetRelayState(string deviceId, [FromQuery, BindRequired] bool on)
        => TypedResults.Ok(await shellyQuerySvc.SetRelayState(deviceId, on));
}
