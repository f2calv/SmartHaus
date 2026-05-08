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
    public async Task<Ok<DoorBirdSnapshot>> GetSnapshot()
        => TypedResults.Ok(await doorBirdQuerySvc.GetSnapshot());

    /// <inheritdoc cref="DoorBirdQueryService.GetRealTimePhoto"/>
    [HttpGet("photo")]
    [Produces("image/jpeg")]
    public async Task<Results<FileContentHttpResult, NotFound<string>>> GetRealTimePhoto()
    {
        var blob = await doorBirdQuerySvc.GetRealTimePhoto();
        return blob.bytes.Length == 0
            ? TypedResults.NotFound("No image returned from DoorBird device.")
            : TypedResults.File(blob.bytes, "image/jpeg");
    }

    /// <inheritdoc cref="DoorBirdQueryService.GetVideoStreamUrl"/>
    [HttpGet("video")]
    public Ok<string> GetVideoStreamUrl()
        => TypedResults.Ok(doorBirdQuerySvc.GetVideoStreamUrl().ToString());

    /// <inheritdoc cref="DoorBirdQueryService.UnlockFrontDoor()"/>
    [HttpPost("relay")]
    public async Task<Ok<bool>> UnlockFrontDoor([FromQuery] string? doorControllerID = null, [FromQuery] string? relayName = null)
        => TypedResults.Ok(await doorBirdQuerySvc.UnlockFrontDoor(doorControllerID, relayName));

    /// <inheritdoc cref="DoorBirdQueryService.LightOn"/>
    [HttpPost("light")]
    public async Task<Ok<LightOnResponse>> LightOn()
        => TypedResults.Ok(await doorBirdQuerySvc.LightOn());

    /// <inheritdoc cref="DoorBirdQueryService.GetInfo"/>
    [HttpGet("info")]
    public async Task<Ok<InfoResponse>> GetInfo()
        => TypedResults.Ok(await doorBirdQuerySvc.GetInfo());

    /// <inheritdoc cref="DoorBirdQueryService.Restart"/>
    [HttpPost("restart")]
    public async Task<Ok<RestartResponse>> Restart()
        => TypedResults.Ok(await doorBirdQuerySvc.Restart());

    /// <inheritdoc cref="DoorBirdQueryService.GetSipStatus"/>
    [HttpGet("sip")]
    public async Task<Ok<SipStatusResponse>> GetSipStatus()
        => TypedResults.Ok(await doorBirdQuerySvc.GetSipStatus());

    /// <inheritdoc cref="DoorBirdQueryService.GetSession"/>
    [HttpGet("session")]
    public async Task<Ok<SessionResponse>> GetSession()
        => TypedResults.Ok(await doorBirdQuerySvc.GetSession());

    /// <inheritdoc cref="DoorBirdQueryService.GetFavorites"/>
    [HttpGet("favorites")]
    public async Task<Ok<string>> GetFavorites()
        => TypedResults.Ok(await doorBirdQuerySvc.GetFavorites());

    /// <inheritdoc cref="DoorBirdQueryService.GetSchedule"/>
    [HttpGet("schedule")]
    public async Task<Ok<string>> GetSchedule()
        => TypedResults.Ok(await doorBirdQuerySvc.GetSchedule());

    /// <inheritdoc cref="DoorBirdQueryService.GetHistoryImage"/>
    [HttpGet("history/image")]
    [Produces("image/jpeg")]
    public async Task<Results<FileContentHttpResult, NotFound<string>>> GetHistoryImage([FromQuery] int index = 1, [FromQuery] DoorBirdEventType? eventType = null)
    {
        var bytes = await doorBirdQuerySvc.GetHistoryImage(index, eventType);
        return bytes is null
            ? TypedResults.NotFound("No history image found.")
            : TypedResults.File(bytes, "image/jpeg");
    }

    /// <inheritdoc cref="DoorBirdQueryService.ListNotifications"/>
    [HttpGet("notifications")]
    public async Task<Ok<NotificationListResponse>> ListNotifications()
        => TypedResults.Ok(await doorBirdQuerySvc.ListNotifications());

    /// <inheritdoc cref="DoorBirdQueryService.SubscribeNotification"/>
    [HttpPost("notifications/subscribe")]
    public async Task<Ok<bool>> SubscribeNotification([FromQuery] string subscriberUrl, [FromQuery] string eventType, [FromQuery] int? relaxation = null)
        => TypedResults.Ok(await doorBirdQuerySvc.SubscribeNotification(subscriberUrl, eventType, relaxation));

    /// <inheritdoc cref="DoorBirdQueryService.UnsubscribeNotification"/>
    [HttpPost("notifications/unsubscribe")]
    public async Task<Ok<bool>> UnsubscribeNotification([FromQuery] string subscriberUrl, [FromQuery] string eventType)
        => TypedResults.Ok(await doorBirdQuerySvc.UnsubscribeNotification(subscriberUrl, eventType));

    #region Event callbacks

    /// <inheritdoc cref="DoorBirdQueryService.SendAlert"/>
    [AllowAnonymous]
    [HttpGet("event/ring")]
    public async Task<Ok<string>> DoorBirdRing()
    {
        await doorBirdQuerySvc.SendAlert(DoorBirdEventType.Doorbell);
        return TypedResults.Ok("ok");
    }

    /// <inheritdoc cref="DoorBirdQueryService.SendAlert"/>
    [AllowAnonymous]
    [HttpGet("event/motion")]
    public async Task<Ok<string>> DoorBirdMotion()
    {
        await doorBirdQuerySvc.SendAlert(DoorBirdEventType.MotionSensor);
        return TypedResults.Ok("ok");
    }

    /// <inheritdoc cref="DoorBirdQueryService.SendAlert"/>
    [AllowAnonymous]
    [HttpGet("event/rfid")]
    public async Task<Ok<string>> DoorBirdRfid()
    {
        await doorBirdQuerySvc.SendAlert(DoorBirdEventType.Rfid);
        return TypedResults.Ok("ok");
    }

    #endregion
}
