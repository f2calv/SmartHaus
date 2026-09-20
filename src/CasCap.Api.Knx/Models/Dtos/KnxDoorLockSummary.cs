namespace CasCap.Models.Dtos;

/// <summary>Summarises the current state of KNX Boolean door-lock sensors.</summary>
public sealed record KnxDoorLockSummary
{
    /// <summary>Total number of classified door locks.</summary>
    [Description("Total number of KNX door-lock sensors.")]
    public int LockCount { get; init; }

    /// <summary>Number of locks reporting locked.</summary>
    [Description("Number of locks reporting Locked (false).")]
    public int LockedCount { get; init; }

    /// <summary>Number of locks reporting unlocked.</summary>
    [Description("Number of locks reporting Unlocked (true).")]
    public int UnlockedCount { get; init; }

    /// <summary>Number of locks for which no current state is available.</summary>
    [Description("Number of locks whose current state is unknown.")]
    public int UnknownCount { get; init; }

    /// <summary>Every classified door lock and its current state.</summary>
    [Description("Door locks. State is Locked, Unlocked, or null when unknown.")]
    public IReadOnlyList<KnxDoorLock> Locks { get; init; } = [];

    /// <summary>Creates a summary from door locks whose states have already been bound.</summary>
    /// <param name="locks">Classified door locks.</param>
    internal static KnxDoorLockSummary Create(IReadOnlyList<KnxDoorLock> locks)
        => new()
        {
            LockCount = locks.Count,
            LockedCount = locks.Count(p => p.State == DptLockState.Locked),
            UnlockedCount = locks.Count(p => p.State == DptLockState.Unlocked),
            UnknownCount = locks.Count(p => p.State is null),
            Locks = locks,
        };
}
