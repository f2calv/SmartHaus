namespace CasCap.Controllers;

/// <summary>
/// REST API controller for Ubiquiti UniFi Protect camera queries and webhook callbacks.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class UbiquitiController(IUbiquitiQueryService ubiquitiQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="UbiquitiQueryService.GetSnapshot"/>
    [HttpGet]
    [ProducesResponseType<UbiquitiSnapshot>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSnapshot()
        => Ok(await ubiquitiQuerySvc.GetSnapshot());

    #region Webhook callbacks

    /// <summary>
    /// Webhook endpoint for UniFi Protect motion detection events.
    /// Configure the camera or controller to POST/GET to this URL when motion is detected.
    /// </summary>
    /// <param name="camera_id">Optional camera identifier from the webhook payload.</param>
    /// <param name="camera_name">Optional camera display name from the webhook payload.</param>
    [AllowAnonymous]
    [HttpGet("event/motion")]
    [HttpPost("event/motion")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> MotionDetected([FromQuery] string? camera_id = null, [FromQuery] string? camera_name = null)
    {
        await ubiquitiQuerySvc.SendAlert(UbiquitiEventType.Motion, camera_id, camera_name);
        return Ok("ok");
    }

    /// <summary>
    /// Webhook endpoint for UniFi Protect smart detection events (person, vehicle, animal, package).
    /// </summary>
    /// <param name="type">The smart detection type. Must be one of: <c>person</c>, <c>vehicle</c>, <c>animal</c>, <c>package</c>.</param>
    /// <param name="camera_id">Optional camera identifier from the webhook payload.</param>
    /// <param name="camera_name">Optional camera display name from the webhook payload.</param>
    /// <param name="score">Optional confidence score (0.0–1.0).</param>
    [AllowAnonymous]
    [HttpGet("event/smart")]
    [HttpPost("event/smart")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SmartDetect(
        [FromQuery] string type,
        [FromQuery] string? camera_id = null,
        [FromQuery] string? camera_name = null,
        [FromQuery] double? score = null)
    {
        var eventType = type?.ToLowerInvariant() switch
        {
            "person" => UbiquitiEventType.SmartDetectPerson,
            "vehicle" => UbiquitiEventType.SmartDetectVehicle,
            "animal" => UbiquitiEventType.SmartDetectAnimal,
            "package" => UbiquitiEventType.SmartDetectPackage,
            _ => (UbiquitiEventType?)null,
        };
        if (eventType is null)
            return BadRequest($"Unknown smart detection type '{type}'. Expected: person, vehicle, animal, package.");

        await ubiquitiQuerySvc.SendAlert(eventType.Value, camera_id, camera_name, score);
        return Ok("ok");
    }

    /// <summary>
    /// Webhook endpoint for UniFi Protect doorbell ring events.
    /// </summary>
    /// <param name="camera_id">Optional camera identifier from the webhook payload.</param>
    /// <param name="camera_name">Optional camera display name from the webhook payload.</param>
    [AllowAnonymous]
    [HttpGet("event/ring")]
    [HttpPost("event/ring")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Ring([FromQuery] string? camera_id = null, [FromQuery] string? camera_name = null)
    {
        await ubiquitiQuerySvc.SendAlert(UbiquitiEventType.Ring, camera_id, camera_name);
        return Ok("ok");
    }

    #endregion
}
