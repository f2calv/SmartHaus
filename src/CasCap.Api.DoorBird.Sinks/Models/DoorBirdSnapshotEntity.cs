namespace CasCap.Models;

/// <summary>
/// Azure Table Storage entity that maintains a running summary of DoorBird activity.
/// A single row is upserted on every event via merge-upsert.
/// </summary>
public class DoorBirdSnapshotEntity : ITableEntity
{
    /// <summary>
    /// Parameterless constructor required by Azure Table Storage deserialization.
    /// </summary>
    public DoorBirdSnapshotEntity() { }

    /// <inheritdoc />
    public string PartitionKey { get; set; } = default!;

    /// <inheritdoc />
    public string RowKey { get; set; } = default!;

    /// <inheritdoc />
    public DateTimeOffset? Timestamp { get; set; }

    /// <inheritdoc />
    public ETag ETag { get; set; }

    /// <summary>UTC timestamp of the last doorbell event, or <see langword="null"/> if none recorded.</summary>
    public DateTimeOffset? LastDoorbellUtc { get; init; }

    /// <summary>UTC timestamp of the last motion sensor event, or <see langword="null"/> if none recorded.</summary>
    public DateTimeOffset? LastMotionUtc { get; init; }

    /// <summary>UTC timestamp of the last RFID event, or <see langword="null"/> if none recorded.</summary>
    public DateTimeOffset? LastRfidUtc { get; init; }

    /// <summary>UTC timestamp of the last door relay trigger, or <see langword="null"/> if none recorded.</summary>
    public DateTimeOffset? LastRelayTriggerUtc { get; init; }

    /// <summary>Total number of doorbell events recorded.</summary>
    public int DoorbellCount { get; init; }

    /// <summary>Total number of motion sensor events recorded.</summary>
    public int MotionCount { get; init; }

    /// <summary>Total number of RFID events recorded.</summary>
    public int RfidCount { get; init; }

    /// <summary>Total number of door relay triggers recorded.</summary>
    public int RelayTriggerCount { get; init; }
}
