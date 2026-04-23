namespace CasCap.Models;

/// <summary>Types of Miele appliance events.</summary>
public enum MieleEventType
{
    /// <summary>General status update from the appliance.</summary>
    StatusUpdate,

    /// <summary>A program has started running.</summary>
    ProgramStarted,

    /// <summary>A program has completed successfully.</summary>
    ProgramComplete,

    /// <summary>The appliance has reported an error.</summary>
    Error,

    /// <summary>Connection to the appliance has been lost.</summary>
    ConnectionLost,
}
