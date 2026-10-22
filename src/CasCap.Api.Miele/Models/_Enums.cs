namespace CasCap.Models;

/// <summary>The provider's outcome for an appliance command.</summary>
public enum MieleCommandResult
{
    /// <summary>The provider accepted the command.</summary>
    Accepted,

    /// <summary>The provider rejected the command for this appliance or its current state.</summary>
    Rejected,

    /// <summary>The appliance was not found.</summary>
    NotFound,
}

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
