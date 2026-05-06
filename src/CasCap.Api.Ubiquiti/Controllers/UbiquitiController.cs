namespace CasCap.Controllers;

/// <summary>
/// REST API controller for Ubiquiti UniFi Protect camera queries and webhook callbacks.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class UbiquitiController(ILogger<UbiquitiController> logger, IUbiquitiQueryService ubiquitiQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="UbiquitiQueryService.GetSnapshot"/>
    [HttpGet]
    public async Task<Ok<UbiquitiSnapshot>> GetSnapshot()
        => TypedResults.Ok(await ubiquitiQuerySvc.GetSnapshot());

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
    public async Task<Ok<string>> MotionDetected([FromQuery] string? camera_id = null, [FromQuery] string? camera_name = null)
    {
        await ubiquitiQuerySvc.SendAlert(UbiquitiEventType.Motion, camera_id, camera_name);
        return TypedResults.Ok("ok");
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
    public async Task<Results<Ok<string>, BadRequest<string>>> SmartDetect(
        [FromQuery] string type,
        [FromQuery] string? camera_id = null,
        [FromQuery] string? camera_name = null,
        [FromQuery] double? score = null)
    {
        string? body = null;
        if (HttpContext.Request.ContentLength > 0)
        {
            using var reader = new StreamReader(HttpContext.Request.Body);
            body = await reader.ReadToEndAsync();
        }

        logger.LogDebug(
            "{ClassName} smart detect webhook received Type={Type}, CameraId={CameraId}, CameraName={CameraName}, Score={Score}, QueryString={QueryString}, Body={Body}",
            nameof(UbiquitiController),
            type,
            camera_id,
            camera_name,
            score,
            HttpContext.Request.QueryString.ToString(),
            body);

        var eventType = type?.ToLowerInvariant() switch
        {
            "person" => UbiquitiEventType.SmartDetectPerson,
            "vehicle" => UbiquitiEventType.SmartDetectVehicle,
            "animal" => UbiquitiEventType.SmartDetectAnimal,
            "package" => UbiquitiEventType.SmartDetectPackage,
            _ => (UbiquitiEventType?)null,
        };
        if (eventType is null)
        {
            logger.LogWarning(
                "{ClassName} unknown smart detect type {Type} for {Method} {Path} with QueryString={QueryString}",
                nameof(UbiquitiController),
                type,
                HttpContext.Request.Method,
                HttpContext.Request.Path,
                HttpContext.Request.QueryString.ToString());
            return TypedResults.BadRequest($"Unknown smart detection type '{type}'. Expected: person, vehicle, animal, package.");
        }

        await ubiquitiQuerySvc.SendAlert(eventType.Value, camera_id, camera_name, score);
        return TypedResults.Ok("ok");
    }

    /// <summary>
    /// Webhook endpoint for UniFi Protect doorbell ring events.
    /// </summary>
    /// <param name="camera_id">Optional camera identifier from the webhook payload.</param>
    /// <param name="camera_name">Optional camera display name from the webhook payload.</param>
    [AllowAnonymous]
    [HttpGet("event/ring")]
    [HttpPost("event/ring")]
    public async Task<Ok<string>> Ring([FromQuery] string? camera_id = null, [FromQuery] string? camera_name = null)
    {
        await ubiquitiQuerySvc.SendAlert(UbiquitiEventType.Ring, camera_id, camera_name);
        return TypedResults.Ok("ok");
    }

    #endregion
}
