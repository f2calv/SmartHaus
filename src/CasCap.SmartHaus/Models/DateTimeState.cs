namespace CasCap.Models;

/// <summary>Snapshot of the current local date/time state for the house.</summary>
public record DateTimeState
{
    /// <summary>Current local date and time.</summary>
    [Description("Local date and time in the house time zone.")]
    public required DateTime LocalTime { get; init; }

    /// <summary>Day of the week (e.g. Monday, Tuesday).</summary>
    [Description("Day of the week, e.g. Monday.")]
    public required string DayOfWeek { get; init; }

    /// <summary>UTC offset as a string (e.g. +02:00:00).</summary>
    [Description("UTC offset of the house time zone, e.g. +02:00:00.")]
    public required string UtcOffset { get; init; }

    /// <summary>IANA time zone identifier.</summary>
    [Description("IANA time zone identifier, e.g. Europe/Berlin.")]
    public required string TimeZone { get; init; }
}
