namespace CasCap.Models;

/// <summary>The result of starting shutter travel, whose completion is reported later in the chat.</summary>
public sealed record ShutterMovementStarted
{
    /// <summary>Identifier of the tracked operation, or <see langword="null"/> when nothing needed to move.</summary>
    [Description("Identifier of the progress-tracking operation; null when nothing needed to move.")]
    public string? OperationId { get; init; }

    /// <summary>The shutters commanded and their targets.</summary>
    [Description("The shutters commanded and their target position and slats.")]
    public required KnxShutterTarget[] Shutters { get; init; }

    /// <summary>What has and has not happened yet, for the model to relay.</summary>
    [Description("What has happened so far. Relay this; do not claim the shutters have finished moving.")]
    public required string Message { get; init; }
}
