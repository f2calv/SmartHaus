namespace CasCap.Models;

/// <summary>Event arguments for message events.</summary>
public class MessageEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="MessageEventArgs"/> class.</summary>
    /// <param name="user">The user who sent the message.</param>
    /// <param name="message">The message content.</param>
    /// <param name="date">The message timestamp.</param>
    public MessageEventArgs(string user, string message, DateTime date)
    {
        this.user = user;
        this.message = message;
        this.date = date;
    }

    /// <summary>The user who sent the message.</summary>
    public string user { get; init; }

    /// <summary>The message content.</summary>
    public string message { get; init; }

    /// <summary>The message timestamp.</summary>
    public DateTime date { get; init; }
}
