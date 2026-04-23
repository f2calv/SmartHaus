namespace CasCap.Models.Dtos;

/// <summary>
/// Represents the result of a single state change attempt for a KNX group address.
/// </summary>
public record KnxStateChangeResult
{
    /// <summary>
    /// The feedback group address name that was polled.
    /// </summary>
    [Description("Feedback group address name that was polled (e.g. EG-LI-Entrance-DL-SW_FB).")]
    public required string GroupAddress { get; init; }

    /// <summary>
    /// The outcome of the state change attempt.
    /// </summary>
    [Description("Outcome of the state change attempt. Values: Queued (queued for background processing), Changed (state changed successfully), AlreadyAtDesiredValue (no change needed), NotChanged (command sent but state did not change), NotFound (group address not found), NoValueProvided (no value in request).")]
    public required StateChangeOutcome Outcome { get; init; }

    /// <summary>
    /// The <see cref="Models.State"/> of the feedback address after the operation, or null if the address was not found.
    /// </summary>
    [Description("State of the feedback address after the operation, or null if the address was not found.")]
    public State? State { get; init; }
}
