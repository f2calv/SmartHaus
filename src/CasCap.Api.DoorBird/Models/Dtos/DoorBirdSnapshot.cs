namespace CasCap.Models.Dtos;

/// <summary>
/// A point-in-time snapshot of recent DoorBird device activity including last event timestamps per <see cref="DoorBirdEventType"/>.
/// </summary>
public record DoorBirdSnapshot
{
    /// <summary>
    /// The UTC timestamp when this snapshot was generated.
    /// </summary>
    [Description("UTC timestamp when the snapshot was generated")]
    public DateTime SnapshotUtc { get; init; }

    /// <summary>
    /// The UTC timestamp of the last doorbell ring event, or <see langword="null"/> if none has been recorded since startup.
    /// </summary>
    [Description("Last doorbell ring (UTC)")]
    public DateTime? LastDoorbellUtc { get; init; }

    /// <summary>
    /// The UTC timestamp of the last motion sensor event, or <see langword="null"/> if none has been recorded since startup.
    /// </summary>
    [Description("Last motion sensor event (UTC)")]
    public DateTime? LastMotionUtc { get; init; }

    /// <summary>
    /// The UTC timestamp of the last RFID event, or <see langword="null"/> if none has been recorded since startup.
    /// </summary>
    [Description("Last RFID event (UTC)")]
    public DateTime? LastRfidUtc { get; init; }

    /// <summary>
    /// The UTC timestamp of the last door relay trigger, or <see langword="null"/> if none has been recorded since startup.
    /// </summary>
    [Description("Last door relay trigger (UTC)")]
    public DateTime? LastRelayTriggerUtc { get; init; }

    /// <summary>
    /// The total number of door relay triggers recorded since the service started.
    /// </summary>
    [Description("Total door relay triggers since startup")]
    public int RelayTriggerCount { get; init; }

    /// <summary>
    /// The total number of doorbell events recorded since the service started.
    /// </summary>
    [Description("Total doorbell events since startup")]
    public int DoorbellCount { get; init; }

    /// <summary>
    /// The total number of motion sensor events recorded since the service started.
    /// </summary>
    [Description("Total motion events since startup")]
    public int MotionCount { get; init; }

    /// <summary>
    /// The total number of RFID events recorded since the service started.
    /// </summary>
    [Description("Total RFID events since startup")]
    public int RfidCount { get; init; }
}
