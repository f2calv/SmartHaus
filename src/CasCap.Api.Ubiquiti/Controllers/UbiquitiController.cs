using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CasCap.Controllers;

/// <summary>
/// REST API controller for Ubiquiti UniFi Protect camera queries and webhook callbacks.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public sealed partial class UbiquitiController(ILogger<UbiquitiController> logger, IUbiquitiQueryService ubiquitiQuerySvc) : ControllerBase
{
    private const long MaxWebhookRequestBytes = 5 * 1024 * 1024;

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
    /// <param name="webhook">Optional UniFi Protect Alarm Manager payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [AllowAnonymous]
    [HttpGet("event/motion")]
    [HttpPost("event/motion")]
    [RequestSizeLimit(MaxWebhookRequestBytes)]
    public async Task<Results<Ok<string>, BadRequest<string>>> MotionDetected(
        [FromQuery] string? camera_id = null,
        [FromQuery] string? camera_name = null,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] UbiquitiWebhookRequest? webhook = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await ubiquitiQuerySvc.SendAlert(
                UbiquitiEventType.Motion,
                camera_id,
                camera_name,
                webhook: webhook,
                cancellationToken: cancellationToken);
            return TypedResults.Ok("ok");
        }
        catch (FormatException)
        {
            LogInvalidWebhook(logger, nameof(UbiquitiController), HttpContext.Request.ContentLength);
            return TypedResults.BadRequest("Invalid UniFi Protect webhook payload.");
        }
    }

    /// <summary>
    /// Webhook endpoint for UniFi Protect smart detection events (person, vehicle, animal, package).
    /// </summary>
    /// <param name="type">The smart detection type. Must be one of: <c>person</c>, <c>vehicle</c>, <c>animal</c>, <c>package</c>.</param>
    /// <param name="camera_id">Optional camera identifier from the webhook payload.</param>
    /// <param name="camera_name">Optional camera display name from the webhook payload.</param>
    /// <param name="score">Optional confidence score (0.0–1.0).</param>
    /// <param name="webhook">Optional UniFi Protect Alarm Manager payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [AllowAnonymous]
    [HttpGet("event/smart")]
    [HttpPost("event/smart")]
    [RequestSizeLimit(MaxWebhookRequestBytes)]
    public async Task<Results<Ok<string>, BadRequest<string>>> SmartDetect(
        [FromQuery] string type,
        [FromQuery] string? camera_id = null,
        [FromQuery] string? camera_name = null,
        [FromQuery] double? score = null,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] UbiquitiWebhookRequest? webhook = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogDebug(
            "{ClassName} smart detect webhook received Type={Type}, CameraId={CameraId}, CameraName={CameraName}, Score={Score}, Method={Method}, ContentType={ContentType}, ContentLength={ContentLength}",
            nameof(UbiquitiController),
            type,
            camera_id,
            camera_name,
            score,
            HttpContext.Request.Method,
            HttpContext.Request.ContentType,
            HttpContext.Request.ContentLength);

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

        try
        {
            await ubiquitiQuerySvc.SendAlert(
                eventType.Value,
                camera_id,
                camera_name,
                score,
                webhook,
                cancellationToken);
            return TypedResults.Ok("ok");
        }
        catch (FormatException)
        {
            LogInvalidWebhook(logger, nameof(UbiquitiController), HttpContext.Request.ContentLength);
            return TypedResults.BadRequest("Invalid UniFi Protect webhook payload.");
        }
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

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{ClassName} rejected an invalid UniFi Protect webhook payload with ContentLength={ContentLength}")]
    private static partial void LogInvalidWebhook(ILogger logger, string className, long? contentLength);
}
