namespace CasCap.Controllers;

/// <summary>
/// REST API controller for DoorBird device queries.
/// </summary>
[Authorize]
[ApiVersion(1.0)]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public class DoorBirdController(IDoorBirdQueryService doorBirdQuerySvc) : ControllerBase
{
    /// <inheritdoc cref="DoorBirdQueryService.GetSnapshot"/>
    [HttpGet]
    [ProducesResponseType<DoorBirdSnapshot>(StatusCodes.Status200OK)]
    public async Task<Ok<DoorBirdSnapshot>> GetSnapshot()
        => TypedResults.Ok(await doorBirdQuerySvc.GetSnapshot());

    /// <inheritdoc cref="DoorBirdQueryService.GetRealTimePhoto"/>
    [HttpGet("photo")]
    [Produces("image/jpeg")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<Results<FileContentHttpResult, NotFound<string>>> GetRealTimePhoto()
    {
        var blob = await doorBirdQuerySvc.GetRealTimePhoto();
        return blob.bytes.Length == 0
            ? TypedResults.NotFound("No image returned from DoorBird device.")
            : TypedResults.File(blob.bytes, "image/jpeg");
    }

    /// <inheritdoc cref="DoorBirdQueryService.GetVideoStreamUrl"/>
    [HttpGet("video")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    public Ok<string> GetVideoStreamUrl()
        => TypedResults.Ok(doorBirdQuerySvc.GetVideoStreamUrl().ToString());

    /// <inheritdoc cref="DoorBirdQueryService.UnlockFrontDoor()"/>
    [HttpPost("relay")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<Ok<bool>> UnlockFrontDoor([FromQuery] string? doorControllerID = null, [FromQuery] string? relayName = null)
        => TypedResults.Ok(await doorBirdQuerySvc.UnlockFrontDoor(doorControllerID, relayName));

    /// <inheritdoc cref="DoorBirdQueryService.LightOn"/>
    [HttpPost("light")]
    [ProducesResponseType<LightOnResponse>(StatusCodes.Status200OK)]
    public async Task<Ok<LightOnResponse>> LightOn()
        => TypedResults.Ok(await doorBirdQuerySvc.LightOn());

    /// <inheritdoc cref="DoorBirdQueryService.GetInfo"/>
    [HttpGet("info")]
    [ProducesResponseType<InfoResponse>(StatusCodes.Status200OK)]
    public async Task<Ok<InfoResponse>> GetInfo()
        => TypedResults.Ok(await doorBirdQuerySvc.GetInfo());

    /// <inheritdoc cref="DoorBirdQueryService.Restart"/>
    [HttpPost("restart")]
    [ProducesResponseType<RestartResponse>(StatusCodes.Status200OK)]
    public async Task<Ok<RestartResponse>> Restart()
        => TypedResults.Ok(await doorBirdQuerySvc.Restart());

    /// <inheritdoc cref="DoorBirdQueryService.GetSipStatus"/>
    [HttpGet("sip")]
    [ProducesResponseType<SipStatusResponse>(StatusCodes.Status200OK)]
    public async Task<Ok<SipStatusResponse>> GetSipStatus()
        => TypedResults.Ok(await doorBirdQuerySvc.GetSipStatus());

    /// <inheritdoc cref="DoorBirdQueryService.GetSession"/>
    [HttpGet("session")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    public async Task<Ok<SessionResponse>> GetSession()
        => TypedResults.Ok(await doorBirdQuerySvc.GetSession());

    /// <inheritdoc cref="DoorBirdQueryService.GetFavorites"/>
    [HttpGet("favorites")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    public async Task<Ok<string>> GetFavorites()
        => TypedResults.Ok(await doorBirdQuerySvc.GetFavorites());

    /// <inheritdoc cref="DoorBirdQueryService.GetSchedule"/>
    [HttpGet("schedule")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    public async Task<Ok<string>> GetSchedule()
        => TypedResults.Ok(await doorBirdQuerySvc.GetSchedule());

    /// <inheritdoc cref="DoorBirdQueryService.GetHistoryImage"/>
    [HttpGet("history/image")]
    [Produces("image/jpeg")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<Results<FileContentHttpResult, NotFound<string>>> GetHistoryImage([FromQuery] int index = 1, [FromQuery] DoorBirdEventType? eventType = null)
    {
        var bytes = await doorBirdQuerySvc.GetHistoryImage(index, eventType);
        return bytes is null
            ? TypedResults.NotFound("No history image found.")
            : TypedResults.File(bytes, "image/jpeg");
    }

    /// <inheritdoc cref="DoorBirdQueryService.ListNotifications"/>
    [HttpGet("notifications")]
    [ProducesResponseType<NotificationListResponse>(StatusCodes.Status200OK)]
    public async Task<Ok<NotificationListResponse>> ListNotifications()
        => TypedResults.Ok(await doorBirdQuerySvc.ListNotifications());

    /// <inheritdoc cref="DoorBirdQueryService.SubscribeNotification"/>
    [HttpPost("notifications/subscribe")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<Ok<bool>> SubscribeNotification([FromQuery] string subscriberUrl, [FromQuery] string eventType, [FromQuery] int? relaxation = null)
        => TypedResults.Ok(await doorBirdQuerySvc.SubscribeNotification(subscriberUrl, eventType, relaxation));

    /// <inheritdoc cref="DoorBirdQueryService.UnsubscribeNotification"/>
    [HttpPost("notifications/unsubscribe")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<Ok<bool>> UnsubscribeNotification([FromQuery] string subscriberUrl, [FromQuery] string eventType)
        => TypedResults.Ok(await doorBirdQuerySvc.UnsubscribeNotification(subscriberUrl, eventType));

    #region Event callbacks

    /// <inheritdoc cref="DoorBirdQueryService.SendAlert"/>
    [AllowAnonymous]
    [HttpGet("event/ring")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<Ok<string>> DoorBirdRing()
    {
        await doorBirdQuerySvc.SendAlert(DoorBirdEventType.Doorbell);
        return TypedResults.Ok("ok");
    }

    /// <inheritdoc cref="DoorBirdQueryService.SendAlert"/>
    [AllowAnonymous]
    [HttpGet("event/motion")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<Ok<string>> DoorBirdMotion()
    {
        await doorBirdQuerySvc.SendAlert(DoorBirdEventType.MotionSensor);
        return TypedResults.Ok("ok");
    }

    /// <inheritdoc cref="DoorBirdQueryService.SendAlert"/>
    [AllowAnonymous]
    [HttpGet("event/rfid")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<Ok<string>> DoorBirdRfid()
    {
        await doorBirdQuerySvc.SendAlert(DoorBirdEventType.Rfid);
        return TypedResults.Ok("ok");
    }

    #endregion
}
