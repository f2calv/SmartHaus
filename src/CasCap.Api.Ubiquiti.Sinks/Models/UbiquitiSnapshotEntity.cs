namespace CasCap.Models;

/// <summary>
/// Azure Table Storage entity that maintains a running summary of Ubiquiti camera activity.
/// A single row is upserted on every event via merge-upsert.
/// </summary>
public class UbiquitiSnapshotEntity : ITableEntity
{
    /// <summary>
    /// Parameterless constructor required by Azure Table Storage deserialization.
    /// </summary>
    public UbiquitiSnapshotEntity() { }

    /// <inheritdoc />
    public string PartitionKey { get; set; } = default!;

    /// <inheritdoc />
    public string RowKey { get; set; } = default!;

    /// <inheritdoc />
    public DateTimeOffset? Timestamp { get; set; }

    /// <inheritdoc />
    public ETag ETag { get; set; }

    /// <summary>UTC timestamp of the last motion detection event, or <see langword="null"/> if none recorded.</summary>
    public DateTimeOffset? LastMotionUtc { get; init; }

    /// <summary>UTC timestamp of the last smart person detection event, or <see langword="null"/> if none recorded.</summary>
    public DateTimeOffset? LastSmartDetectPersonUtc { get; init; }

    /// <summary>UTC timestamp of the last smart vehicle detection event, or <see langword="null"/> if none recorded.</summary>
    public DateTimeOffset? LastSmartDetectVehicleUtc { get; init; }

    /// <summary>UTC timestamp of the last smart animal detection event, or <see langword="null"/> if none recorded.</summary>
    public DateTimeOffset? LastSmartDetectAnimalUtc { get; init; }

    /// <summary>UTC timestamp of the last smart package detection event, or <see langword="null"/> if none recorded.</summary>
    public DateTimeOffset? LastSmartDetectPackageUtc { get; init; }

    /// <summary>UTC timestamp of the last doorbell ring event, or <see langword="null"/> if none recorded.</summary>
    public DateTimeOffset? LastRingUtc { get; init; }

    /// <summary>Total number of motion detection events recorded.</summary>
    public int MotionCount { get; init; }

    /// <summary>Total number of smart person detection events recorded.</summary>
    public int SmartDetectPersonCount { get; init; }

    /// <summary>Total number of smart vehicle detection events recorded.</summary>
    public int SmartDetectVehicleCount { get; init; }

    /// <summary>Total number of smart animal detection events recorded.</summary>
    public int SmartDetectAnimalCount { get; init; }

    /// <summary>Total number of smart package detection events recorded.</summary>
    public int SmartDetectPackageCount { get; init; }

    /// <summary>Total number of doorbell ring events recorded.</summary>
    public int RingCount { get; init; }
}
