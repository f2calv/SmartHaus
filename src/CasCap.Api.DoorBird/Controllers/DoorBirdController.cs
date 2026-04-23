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
    public async Task<IActionResult> GetSnapshot()
        => Ok(await doorBirdQuerySvc.GetSnapshot());

    /// <inheritdoc cref="DoorBirdQueryService.GetRealTimePhoto"/>
    [HttpGet("photo")]
    [Produces("image/jpeg")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRealTimePhoto()
    {
        var blob = await doorBirdQuerySvc.GetRealTimePhoto();
        return blob.bytes.Length == 0
            ? NotFound("No image returned from DoorBird device.")
            : File(blob.bytes, "image/jpeg");
    }

    /// <inheritdoc cref="DoorBirdQueryService.GetVideoStreamUrl"/>
    [HttpGet("video")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    public IActionResult GetVideoStreamUrl()
        => Ok(doorBirdQuerySvc.GetVideoStreamUrl().ToString());

    /// <inheritdoc cref="DoorBirdQueryService.UnlockFrontDoor()"/>
    [HttpPost("relay")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UnlockFrontDoor([FromQuery] string? doorControllerID = null, [FromQuery] string? relayName = null)
    {
        var result = await doorBirdQuerySvc.UnlockFrontDoor(doorControllerID, relayName);
        return Ok(result);
    }

    /// <inheritdoc cref="DoorBirdQueryService.LightOn"/>
    [HttpPost("light")]
    [ProducesResponseType<LightOnResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> LightOn()
    {
        var result = await doorBirdQuerySvc.LightOn();
        return Ok(result);
    }

    /// <inheritdoc cref="DoorBirdQueryService.GetInfo"/>
    [HttpGet("info")]
    [ProducesResponseType<InfoResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInfo()
    {
        var info = await doorBirdQuerySvc.GetInfo();
        return Ok(info);
    }

    /// <inheritdoc cref="DoorBirdQueryService.Restart"/>
    [HttpPost("restart")]
    [ProducesResponseType<RestartResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Restart()
    {
        var result = await doorBirdQuerySvc.Restart();
        return Ok(result);
    }

    /// <inheritdoc cref="DoorBirdQueryService.GetSipStatus"/>
    [HttpGet("sip")]
    [ProducesResponseType<SipStatusResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSipStatus()
    {
        var result = await doorBirdQuerySvc.GetSipStatus();
        return Ok(result);
    }

    /// <inheritdoc cref="DoorBirdQueryService.GetSession"/>
    [HttpGet("session")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSession()
    {
        var result = await doorBirdQuerySvc.GetSession();
        return Ok(result);
    }

    /// <inheritdoc cref="DoorBirdQueryService.GetFavorites"/>
    [HttpGet("favorites")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFavorites()
    {
        var result = await doorBirdQuerySvc.GetFavorites();
        return Ok(result);
    }

    /// <inheritdoc cref="DoorBirdQueryService.GetSchedule"/>
    [HttpGet("schedule")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchedule()
    {
        var result = await doorBirdQuerySvc.GetSchedule();
        return Ok(result);
    }

    /// <inheritdoc cref="DoorBirdQueryService.GetHistoryImage"/>
    [HttpGet("history/image")]
    [Produces("image/jpeg")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistoryImage([FromQuery] int index = 1, [FromQuery] DoorBirdEventType? eventType = null)
    {
        var bytes = await doorBirdQuerySvc.GetHistoryImage(index, eventType);
        return bytes is null
            ? NotFound("No history image found.")
            : File(bytes, "image/jpeg");
    }

    /// <inheritdoc cref="DoorBirdQueryService.ListNotifications"/>
    [HttpGet("notifications")]
    [ProducesResponseType<NotificationListResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListNotifications()
    {
        var result = await doorBirdQuerySvc.ListNotifications();
        return Ok(result);
    }

    /// <inheritdoc cref="DoorBirdQueryService.SubscribeNotification"/>
    [HttpPost("notifications/subscribe")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SubscribeNotification([FromQuery] string subscriberUrl, [FromQuery] string eventType, [FromQuery] int? relaxation = null)
    {
        var result = await doorBirdQuerySvc.SubscribeNotification(subscriberUrl, eventType, relaxation);
        return Ok(result);
    }

    /// <inheritdoc cref="DoorBirdQueryService.UnsubscribeNotification"/>
    [HttpPost("notifications/unsubscribe")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UnsubscribeNotification([FromQuery] string subscriberUrl, [FromQuery] string eventType)
    {
        var result = await doorBirdQuerySvc.UnsubscribeNotification(subscriberUrl, eventType);
        return Ok(result);
    }

    #region Event callbacks

    /// <inheritdoc cref="DoorBirdQueryService.SendAlert"/>
    [AllowAnonymous]
    [HttpGet("event/ring")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DoorBirdRing()
    {
        await doorBirdQuerySvc.SendAlert(DoorBirdEventType.Doorbell);
        return Ok("ok");
    }

    /// <inheritdoc cref="DoorBirdQueryService.SendAlert"/>
    [AllowAnonymous]
    [HttpGet("event/motion")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DoorBirdMotion()
    {
        await doorBirdQuerySvc.SendAlert(DoorBirdEventType.MotionSensor);
        return Ok("ok");
    }

    /// <inheritdoc cref="DoorBirdQueryService.SendAlert"/>
    [AllowAnonymous]
    [HttpGet("event/rfid")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DoorBirdRfid()
    {
        await doorBirdQuerySvc.SendAlert(DoorBirdEventType.Rfid);
        return Ok("ok");
    }

    #endregion
}
