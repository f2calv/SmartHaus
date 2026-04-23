namespace CasCap.Models.Dtos;

/// <summary>
/// Response payload containing the results of one or more KNX state change attempts.
/// </summary>
public record KnxStateChangeResponse
{
    /// <summary>
    /// Unique identifier for this state change operation.
    /// </summary>
    [Description("Unique identifier for this state change operation.")]
    public required Guid ChangeId { get; init; }

    /// <summary>
    /// Indicates whether the response is from a dry run (preview only, no commands sent).
    /// </summary>
    [Description("True when this response is from a dry run preview; false when commands were actually sent.")]
    public bool IsDryRun { get; init; }

    /// <summary>
    /// Indicates whether all results completed without issues. Returns <see langword="true"/>
    /// when every <see cref="KnxStateChangeResult.Outcome"/> is
    /// <see cref="StateChangeOutcome.Queued"/>, <see cref="StateChangeOutcome.Changed"/>,
    /// <see cref="StateChangeOutcome.DryRun"/>
    /// or <see cref="StateChangeOutcome.AlreadyAtDesiredValue"/>;
    /// <see langword="false"/> if any result has a failure outcome such as
    /// <see cref="StateChangeOutcome.NotFound"/>, <see cref="StateChangeOutcome.NotChanged"/>
    /// or <see cref="StateChangeOutcome.NoValueProvided"/>.
    /// </summary>
    [Description("True when all state changes succeeded or were already at desired value; false if any failed or were not found.")]
    public bool Ok => Results.Length > 0 && Results.All(r => r.Outcome
        is StateChangeOutcome.Queued
        or StateChangeOutcome.Changed
        or StateChangeOutcome.DryRun
        or StateChangeOutcome.AlreadyAtDesiredValue);

    /// <summary>
    /// The elapsed time in milliseconds taken to process and resolve the state change request.
    /// </summary>
    [Description("Elapsed processing time in milliseconds.")]
    public required long DurationMs { get; init; }

    /// <summary>
    /// The individual state change results for each group address affected.
    /// </summary>
    [Description("Individual state change results for each group address affected.")]
    public required KnxStateChangeResult[] Results { get; init; }
}
