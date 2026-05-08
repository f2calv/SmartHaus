using CasCap.Common.Services;

namespace CasCap.Services;

/// <summary>
/// HTTP client for the DoorBird LAN API.
/// </summary>
/// <remarks>
/// See <see href="https://www.doorbird.com/downloads/api_lan.pdf?rev=0.36"/> for the full API specification.
/// Our device model is a DoorBird D2101V, but this client should work with any DoorBird device that supports the LAN API.
/// </remarks>
public class DoorBirdClientService : HttpClientBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DoorBirdClientService"/> class.
    /// </summary>
    public DoorBirdClientService(ILogger<DoorBirdClientService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        Client = httpClientFactory.CreateClient(nameof(DoorBirdConnectionHealthCheck));
    }

    #region Session

    /// <summary>
    /// Creates a new authenticated session on the DoorBird device.
    /// </summary>
    public async Task<SessionResponse?> GetSession()
    {
        var requestUri = "bha-api/getsession.cgi";
        try
        {
            var tpl = await base.GetAsync<SessionResponse, object>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get session", nameof(DoorBirdClientService));
        }
        return null;
    }

    /// <summary>
    /// Invalidates an existing session on the DoorBird device.
    /// </summary>
    /// <param name="sessionId">The session ID to invalidate.</param>
    public async Task<SessionResponse?> InvalidateSession(string sessionId)
    {
        var requestUri = $"bha-api/getsession.cgi?invalidate={sessionId}";
        try
        {
            var tpl = await base.GetAsync<SessionResponse, object>(requestUri);
            _logger.LogInformation("{ClassName} session '{SessionId}' invalidated", nameof(DoorBirdClientService), sessionId);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to invalidate session", nameof(DoorBirdClientService));
        }
        return null;
    }

    #endregion

    #region Live image & video

    /// <inheritdoc cref="DoorBirdQueryService.GetRealTimePhoto"/>
    public async Task<byte[]?> GetImage()
    {
        var requestUri = "bha-api/image.cgi";
        try
        {
            var tpl = await base.GetAsync<byte[], object>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get image", nameof(DoorBirdClientService));
        }
        return null;
    }

    /// <inheritdoc cref="DoorBirdQueryService.GetVideoStreamUrl"/>
    /// <remarks>
    /// The returned URL can be opened in a browser or consumed as a multipart MJPEG stream.
    /// The stream runs until the client disconnects.
    /// </remarks>
    public Uri GetVideoStreamUrl()
    {
        return new Uri(Client.BaseAddress!, "bha-api/video.cgi");
    }

    #endregion

    #region Door & peripherals

    /// <inheritdoc cref="DoorBirdQueryService.UnlockFrontDoor()"/>
    /// <param name="doorControllerID">Optional relay or door controller identifier (e.g. "1", "2", or a controller ID).</param>
    /// <param name="relayName">Optional relay name when using a door controller (e.g. "1").</param>
    /// <returns><see langword="true"/> if the relay was triggered successfully.</returns>
    public async Task<bool> TriggerRelay(string? doorControllerID = null, string? relayName = null)
    {
        var requestUri = "/bha-api/open-door.cgi";
        if (!string.IsNullOrWhiteSpace(doorControllerID))
        {
            if (string.IsNullOrWhiteSpace(relayName))
                requestUri += $"?r={doorControllerID}";//1|2|<doorcontrollerID>
            else
                requestUri += $"?r={doorControllerID}@{relayName}";//1|2|<doorcontrollerID>@<relay>
        }
        try
        {
            _ = await base.GetAsync<string, object>(requestUri);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to trigger relay", nameof(DoorBirdClientService));
        }
        return false;
    }

    /// <inheritdoc cref="DoorBirdQueryService.LightOn"/>
    public async Task<LightOnResponse?> LightOn()
    {
        var requestUri = "/bha-api/light-on.cgi";
        try
        {
            var tpl = await base.GetAsync<LightOnResponse, object>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to turn light on", nameof(DoorBirdClientService));
        }
        return null;
    }

    #endregion

    #region Device info & configuration

    /// <summary>
    /// Retrieves device information including firmware version, build number, MAC address and available relays.
    /// </summary>
    public async Task<InfoResponse?> GetInfo()
    {
        var requestUri = "bha-api/info.cgi";
        try
        {
            var tpl = await base.GetAsync<InfoResponse, object>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get info", nameof(DoorBirdClientService));
        }
        return null;
    }

    /// <summary>
    /// Restarts (reboots) the DoorBird device.
    /// </summary>
    public async Task<RestartResponse?> Restart()
    {
        var requestUri = "bha-api/restart.cgi";
        try
        {
            var tpl = await base.GetAsync<RestartResponse, object>(requestUri);
            _logger.LogWarning("{ClassName} device restart requested", nameof(DoorBirdClientService));
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to restart device", nameof(DoorBirdClientService));
        }
        return null;
    }

    /// <summary>
    /// Retrieves the SIP status and configuration from the DoorBird device.
    /// </summary>
    public async Task<SipStatusResponse?> GetSipStatus()
    {
        var requestUri = "bha-api/sip.cgi";
        try
        {
            var tpl = await base.GetAsync<SipStatusResponse, object>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get SIP status", nameof(DoorBirdClientService));
        }
        return null;
    }

    #endregion

    #region Favorites & schedule

    /// <summary>
    /// Retrieves the configured favorites (HTTP and SIP notification endpoints) from the DoorBird device.
    /// </summary>
    public async Task<string?> GetFavorites()
    {
        var requestUri = "bha-api/favorites.cgi";
        try
        {
            var tpl = await base.GetAsync<string, object>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get favorites", nameof(DoorBirdClientService));
        }
        return null;
    }

    /// <summary>
    /// Retrieves the schedule configuration from the DoorBird device.
    /// </summary>
    public async Task<string?> GetSchedule()
    {
        var requestUri = "bha-api/schedule.cgi";
        try
        {
            var tpl = await base.GetAsync<string, object>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get schedule", nameof(DoorBirdClientService));
        }
        return null;
    }

    #endregion

    #region History

    /// <inheritdoc cref="DoorBirdQueryService.GetHistoryImage"/>
    public async Task<byte[]?> GetHistoryImage(int index = 1, DoorBirdEventType? doorBirdEventType = null)
    {
        var qs = new List<KeyValuePair<string, string?>>();
        if (index < 1 || index > 50)
            throw new ArgumentException("index must be between 1 and 50", nameof(index));
        else
            qs.Add(KeyValuePair.Create<string, string?>("index", index.ToString()));
        if (doorBirdEventType is not null)
            qs.Add(KeyValuePair.Create<string, string?>("event", doorBirdEventType.ToString()!.ToLowerInvariant()));
        var requestUri = $"bha-api/history.cgi{QueryString.Create(qs)}";
        try
        {
            var tpl = await base.GetAsync<byte[], object>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to get history image", nameof(DoorBirdClientService));
        }
        return null;
    }

    #endregion

    #region Notifications

    /// <summary>
    /// Lists all HTTP notification subscribers registered on the DoorBird device.
    /// </summary>
    public async Task<NotificationListResponse?> ListNotifications()
    {
        var requestUri = "bha-api/notification.cgi?http";
        try
        {
            var tpl = await base.GetAsync<NotificationListResponse, object>(requestUri);
            return tpl.result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to list notifications", nameof(DoorBirdClientService));
        }
        return null;
    }

    /// <summary>
    /// Subscribes an HTTP URL to receive notifications for a specific event type.
    /// </summary>
    /// <param name="subscriberUrl">The URL that the DoorBird device will call when the event occurs.</param>
    /// <param name="eventType">The event type to subscribe to (e.g. "doorbell", "motionsensor").</param>
    /// <param name="relaxation">Optional minimum interval in seconds between notifications (default is device-specific).</param>
    /// <returns><see langword="true"/> if the subscription was created successfully.</returns>
    public async Task<bool> SubscribeNotification(string subscriberUrl, string eventType, int? relaxation = null)
    {
        var qs = new List<KeyValuePair<string, string?>>
        {
            KeyValuePair.Create<string, string?>("url", subscriberUrl),
            KeyValuePair.Create<string, string?>("event", eventType)
        };
        if (relaxation.HasValue)
            qs.Add(KeyValuePair.Create<string, string?>("relaxation", relaxation.Value.ToString()));
        var requestUri = $"bha-api/notification.cgi{QueryString.Create(qs)}&subscribe=1";
        try
        {
            _ = await base.GetAsync<string, object>(requestUri);
            _logger.LogInformation("{ClassName} notification subscribed: event={EventType}, url={Url}",
                nameof(DoorBirdClientService), eventType, subscriberUrl);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to subscribe notification", nameof(DoorBirdClientService));
        }
        return false;
    }

    /// <summary>
    /// Unsubscribes an HTTP URL from receiving notifications for a specific event type.
    /// </summary>
    /// <param name="subscriberUrl">The subscriber URL to remove.</param>
    /// <param name="eventType">The event type to unsubscribe from.</param>
    /// <returns><see langword="true"/> if the subscription was removed successfully.</returns>
    public async Task<bool> UnsubscribeNotification(string subscriberUrl, string eventType)
    {
        var qs = new List<KeyValuePair<string, string?>>
        {
            KeyValuePair.Create<string, string?>("url", subscriberUrl),
            KeyValuePair.Create<string, string?>("event", eventType)
        };
        var requestUri = $"bha-api/notification.cgi{QueryString.Create(qs)}&subscribe=0";
        try
        {
            _ = await base.GetAsync<string, object>(requestUri);
            _logger.LogInformation("{ClassName} notification unsubscribed: event={EventType}, url={Url}",
                nameof(DoorBirdClientService), eventType, subscriberUrl);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{ClassName} failed to unsubscribe notification", nameof(DoorBirdClientService));
        }
        return false;
    }

    #endregion
}
