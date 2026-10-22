using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CasCap.Controllers;

/// <summary>
/// REST API controller for Ubiquiti UniFi Protect camera queries and webhook callbacks.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class UbiquitiController(IUbiquitiQueryService ubiquitiQuerySvc) : ControllerBase
{
    private const long MaxWebhookRequestBytes = 5 * 1024 * 1024;

    /// <inheritdoc cref="UbiquitiQueryService.GetSnapshot"/>
    [HttpGet]
    public async Task<Ok<UbiquitiSnapshot>> GetSnapshot()
        => TypedResults.Ok(await ubiquitiQuerySvc.GetSnapshot());

    #region Webhook callbacks

    /// <inheritdoc cref="UbiquitiQueryService.SendAlert"/>
    [AllowAnonymous]
    [HttpGet("event/motion")]
    [HttpPost("event/motion")]
    [RequestSizeLimit(MaxWebhookRequestBytes)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<Results<Ok<string>, BadRequest<string>>> MotionDetected(
        [FromQuery] string? camera_id = null,
        [FromQuery] string? camera_name = null,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] UbiquitiWebhookRequest? webhook = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ubiquitiQuerySvc.SendAlert(
            UbiquitiEventType.Motion, camera_id, camera_name, webhook: webhook, cancellationToken: cancellationToken);
        return result.Error is { } error ? TypedResults.BadRequest(error) : TypedResults.Ok("ok");
    }

    /// <inheritdoc cref="UbiquitiQueryService.SendSmartDetectionAlert"/>
    [AllowAnonymous]
    [HttpGet("event/smart")]
    [HttpPost("event/smart")]
    [RequestSizeLimit(MaxWebhookRequestBytes)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<Results<Ok<string>, BadRequest<string>>> SmartDetect(
        [FromQuery] string type,
        [FromQuery] string? camera_id = null,
        [FromQuery] string? camera_name = null,
        [FromQuery] double? score = null,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] UbiquitiWebhookRequest? webhook = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ubiquitiQuerySvc.SendSmartDetectionAlert(type, camera_id, camera_name, score, webhook, cancellationToken);
        return result.Error is { } error ? TypedResults.BadRequest(error) : TypedResults.Ok("ok");
    }

    /// <summary>
    /// Webhook endpoint for UniFi Protect doorbell ring events.
    /// </summary>
    /// <param name="camera_id">Optional camera identifier from the webhook payload.</param>
    /// <param name="camera_name">Optional camera display name from the webhook payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [AllowAnonymous]
    [HttpGet("event/ring")]
    [HttpPost("event/ring")]
    public async Task<Results<Ok<string>, BadRequest<string>>> Ring(
        [FromQuery] string? camera_id = null,
        [FromQuery] string? camera_name = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ubiquitiQuerySvc.SendAlert(UbiquitiEventType.Ring, camera_id, camera_name, cancellationToken: cancellationToken);
        return result.Error is { } error ? TypedResults.BadRequest(error) : TypedResults.Ok("ok");
    }

    #endregion

}
