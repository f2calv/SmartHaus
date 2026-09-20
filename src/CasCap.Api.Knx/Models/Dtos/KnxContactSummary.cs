namespace CasCap.Models.Dtos;

/// <summary>Summarises the current binary state of physical door and window contacts.</summary>
public sealed record KnxContactSummary
{
    /// <summary>Total number of physical contacts with a room-scoped KNX address.</summary>
    [Description("Total number of physical door and window contacts. Aggregate KNX addresses are excluded.")]
    public int ContactCount { get; init; }

    /// <summary>Number of contacts reporting an open state.</summary>
    [Description("Number of physically open doors and windows.")]
    public int OpenCount { get; init; }

    /// <summary>Number of contacts reporting a closed state.</summary>
    [Description("Number of physically closed doors and windows.")]
    public int ClosedCount { get; init; }

    /// <summary>Number of contacts for which no current state is available.</summary>
    [Description("Number of door and window contacts whose current state is unknown.")]
    public int UnknownCount { get; init; }

    /// <summary>Every physical contact and its current binary state.</summary>
    [Description("Physical contacts. State is Active (open), Inactive (closed), or null (unknown); contacts are never partially open.")]
    public IReadOnlyList<KnxContact> Contacts { get; init; } = [];

    /// <summary>Creates a summary from physical contacts whose states have already been bound.</summary>
    /// <param name="contacts">Physical door and window contacts.</param>
    internal static KnxContactSummary Create(IReadOnlyList<KnxContact> contacts)
        => new()
        {
            ContactCount = contacts.Count,
            OpenCount = contacts.Count(p => p.State == DptState.Active),
            ClosedCount = contacts.Count(p => p.State == DptState.Inactive),
            UnknownCount = contacts.Count(p => p.State is null),
            Contacts = contacts,
        };
}