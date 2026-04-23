namespace CasCap.Abstractions;

/// <summary>
/// Methods that clients can invoke on the HausHub server.
/// All real-time event types from each feature are published through this single interface.
/// The four event methods carry default implementations so that downstream sink assemblies
/// can use <c>nameof(IHausServerHub.SendBuderusEvent)</c> etc. without referencing the
/// concrete event types defined in the per-feature projects.
/// </summary>
public interface IHausServerHub
{
    /// <summary>Broadcasts a text message to all other connected clients.</summary>
    Task SendMessage(string user, string message, DateTime date);

    /// <summary>Broadcasts a text message to all connected clients.</summary>
    Task Broadcast(string message);

    /// <summary>Broadcasts a <see cref="BuderusEvent"/> to all connected clients.</summary>
    Task SendBuderusEvent() => throw new NotImplementedException();

    /// <summary>Broadcasts a <see cref="DoorBirdEvent"/> to all connected clients.</summary>
    Task SendDoorBirdEvent() => throw new NotImplementedException();

    /// <summary>Broadcasts a <see cref="FroniusEvent"/> to all connected clients.</summary>
    Task SendFroniusEvent() => throw new NotImplementedException();

    /// <summary>Broadcasts a <c>KnxTelegram</c> to all connected clients.</summary>
    Task SendKnxTelegram() => throw new NotImplementedException();
}
