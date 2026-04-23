namespace CasCap.Models.Dtos;

/// <summary>
/// A point-in-time snapshot of recent Ubiquiti UniFi Protect camera activity including last event timestamps per <see cref="UbiquitiEventType"/>.
/// </summary>
public record UbiquitiSnapshot
{
    /// <summary>
    /// The UTC timestamp when this snapshot was generated.
    /// </summary>
    [Description("UTC timestamp when the snapshot was generated")]
    public DateTime SnapshotUtc { get; init; }

    /// <summary>
    /// The UTC timestamp of the last motion detection event, or <see langword="null"/> if none recorded since startup.
    /// </summary>
    [Description("Last motion detection event (UTC)")]
    public DateTime? LastMotionUtc { get; init; }

    /// <summary>
    /// The UTC timestamp of the last smart detection (person) event, or <see langword="null"/> if none recorded since startup.
    /// </summary>
    [Description("Last smart person detection event (UTC)")]
    public DateTime? LastSmartDetectPersonUtc { get; init; }

    /// <summary>
    /// The UTC timestamp of the last smart detection (vehicle) event, or <see langword="null"/> if none recorded since startup.
    /// </summary>
    [Description("Last smart vehicle detection event (UTC)")]
    public DateTime? LastSmartDetectVehicleUtc { get; init; }

    /// <summary>
    /// The UTC timestamp of the last smart detection (animal) event, or <see langword="null"/> if none recorded since startup.
    /// </summary>
    [Description("Last smart animal detection event (UTC)")]
    public DateTime? LastSmartDetectAnimalUtc { get; init; }

    /// <summary>
    /// The UTC timestamp of the last smart detection (package) event, or <see langword="null"/> if none recorded since startup.
    /// </summary>
    [Description("Last smart package detection event (UTC)")]
    public DateTime? LastSmartDetectPackageUtc { get; init; }

    /// <summary>
    /// The UTC timestamp of the last doorbell ring event, or <see langword="null"/> if none recorded since startup.
    /// </summary>
    [Description("Last doorbell ring event (UTC)")]
    public DateTime? LastRingUtc { get; init; }

    /// <summary>
    /// The total number of motion detection events recorded since the service started.
    /// </summary>
    [Description("Total motion events since startup")]
    public int MotionCount { get; init; }

    /// <summary>
    /// The total number of smart person detection events recorded since the service started.
    /// </summary>
    [Description("Total smart person detection events since startup")]
    public int SmartDetectPersonCount { get; init; }

    /// <summary>
    /// The total number of smart vehicle detection events recorded since the service started.
    /// </summary>
    [Description("Total smart vehicle detection events since startup")]
    public int SmartDetectVehicleCount { get; init; }

    /// <summary>
    /// The total number of smart animal detection events recorded since the service started.
    /// </summary>
    [Description("Total smart animal detection events since startup")]
    public int SmartDetectAnimalCount { get; init; }

    /// <summary>
    /// The total number of smart package detection events recorded since the service started.
    /// </summary>
    [Description("Total smart package detection events since startup")]
    public int SmartDetectPackageCount { get; init; }

    /// <summary>
    /// The total number of doorbell ring events recorded since the service started.
    /// </summary>
    [Description("Total doorbell ring events since startup")]
    public int RingCount { get; init; }
}
