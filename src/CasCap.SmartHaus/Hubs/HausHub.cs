using Microsoft.AspNetCore.Authorization;

namespace CasCap.Hubs;

/// <summary>
/// Consolidated SignalR hub that provides a single real-time event endpoint for all features
/// (Fronius, KNX, DoorBird, Buderus). Can be deployed as a standalone pod for separation of
/// responsibility, with all feature pods connecting to it as clients.
/// </summary>
[Authorize]
public class HausHub(ILogger<HausHub> logger, IOptions<AppConfig> appConfig,
    IEnumerable<IEventSink<HubEvent>> hubSinks) : Hub<IHausClientHub>, IHausServerHub
{

    /// <inheritdoc/>
    public Task SendMessage(string user, string message, DateTime date)
        => Clients.AllExcept(Context.ConnectionId).ReceiveMessage(user, message, date);

    /// <summary>Broadcasts a <see cref="FroniusEvent"/> to all connected clients.</summary>
    public async Task SendFroniusEvent(FroniusEvent e)
    {
        await Clients.All.ReceiveFroniusEvent(e);
        await WriteHubEventAsync(nameof(FroniusEvent));
    }

    /// <summary>Broadcasts a <see cref="KnxEvent"/> to all connected clients.</summary>
    public async Task SendKnxTelegram(KnxEvent e)
    {
        await Clients.All.ReceiveKnxEvent(e);
        await WriteHubEventAsync(nameof(KnxEvent));
    }

    /// <summary>Broadcasts a <see cref="DoorBirdEvent"/> to all connected clients.</summary>
    public async Task SendDoorBirdEvent(DoorBirdEvent e)
    {
        await Clients.All.ReceiveDoorBirdEvent(e);
        await WriteHubEventAsync(nameof(DoorBirdEvent));
    }

    /// <summary>Broadcasts a <see cref="BuderusEvent"/> to all connected clients.</summary>
    public async Task SendBuderusEvent(BuderusEvent e)
    {
        await Clients.All.ReceiveBuderusEvent(e);
        await WriteHubEventAsync(nameof(BuderusEvent));
    }

    /// <inheritdoc/>
    public Task Broadcast(string message)
        => Clients.All.ReceiveMessage(appConfig.Value.PodName ?? AppDomain.CurrentDomain.FriendlyName, message, DateTime.UtcNow);

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "SignalR Users");
        logger.LogInformation("{ClassName} client {ConnectionId} joined the hub", nameof(HausHub), Context.ConnectionId);
        await Clients.All.ReceiveMessage(appConfig.Value.PodName ?? AppDomain.CurrentDomain.FriendlyName, $"{Context.ConnectionId} connected to the hub", DateTime.UtcNow);
        await base.OnConnectedAsync();
    }

    #region private helpers

    private Task WriteHubEventAsync(string eventType)
    {
        var hubEvent = new HubEvent(eventType, DateTimeOffset.UtcNow);
        return Task.WhenAll(hubSinks.Select(s => s.WriteEvent(hubEvent)));
    }

    #endregion
}
