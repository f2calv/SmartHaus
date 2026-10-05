namespace CasCap.Abstractions;

/// <summary>
/// Methods the server can invoke on <see cref="Hubs.HausHub"/> clients.
/// All real-time event types from each feature are delivered through this single interface.
/// </summary>
public interface IHausClientHub
{
    /// <summary>Delivers a text message from the specified user.</summary>
    Task ReceiveMessage(string user, string message, DateTime date);

    /// <summary>Delivers a <see cref="FroniusEvent"/> to the client.</summary>
    Task ReceiveFroniusEvent(FroniusEvent e);

    /// <summary>Delivers a <see cref="KnxEvent"/> to the client.</summary>
    Task ReceiveKnxEvent(KnxEvent e);

    /// <summary>Delivers a <see cref="DoorBirdEvent"/> to the client.</summary>
    Task ReceiveDoorBirdEvent(DoorBirdEvent e);

    /// <summary>Delivers a <see cref="BuderusEvent"/> to the client.</summary>
    Task ReceiveBuderusEvent(BuderusEvent e);
}
