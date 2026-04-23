namespace CasCap.Models;

/// <summary>
/// Setting keys specific to the KNX feature sinks.
/// </summary>
public static class KnxSinkKeys
{
    /// <summary>Redis hash key for decoded strings.</summary>
    public const string SnapshotStrings = nameof(SnapshotStrings);

    /// <summary>Redis hash key for timestamps.</summary>
    public const string SnapshotTimestamps = nameof(SnapshotTimestamps);

    /// <summary>Azure Tables CEMI raw data table name.</summary>
    public const string CemiTableName = nameof(CemiTableName);
}
