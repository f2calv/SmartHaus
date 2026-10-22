namespace CasCap.Models;

/// <summary>Describes an accepted datapoint write or its validation/device rejection.</summary>
public sealed record BuderusDataPointWriteResult
{
    /// <summary>Gets the rejection message, or null when the write was accepted.</summary>
    /// <example>The datapoint is not writeable.</example>
    public string? Error { get; init; }

    /// <summary>Gets whether the device accepted the write.</summary>
    /// <example>true</example>
    public bool Succeeded => Error is null;
}
