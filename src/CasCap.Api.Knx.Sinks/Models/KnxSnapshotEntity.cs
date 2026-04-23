namespace CasCap.Models;

/// <summary>
/// Azure Table Storage snapshot entity storing the latest value per KNX group address.
/// </summary>
public class KnxSnapshotEntity : ITableEntity
{
    /// <inheritdoc/>
    public KnxSnapshotEntity() { }

    /// <summary>Initialises a new instance from a <see cref="KnxEvent"/> and running count.</summary>
    public KnxSnapshotEntity(string partitionKey, KnxEvent knxEvent, long Count)
    {
        PartitionKey = partitionKey;
        RowKey = knxEvent.Kga.Name;
        ia = knxEvent.Args.SourceAddress.IndividualAddress;
        ga = knxEvent.Kga.GroupAddress;
        v = knxEvent.ValueAsString;
        s = knxEvent.ValueLabel;
        dt = new DateTimeOffset(knxEvent.TimestampUtc, TimeSpan.Zero);
        c = Count;
    }

    /// <summary>Snapshot partition key.</summary>
    public string PartitionKey { get; set; } = default!;

    /// <inheritdoc cref="KnxGroupAddressParsed.Name" />
    public string RowKey { get; set; } = default!;

    /// <inheritdoc />
    public DateTimeOffset? Timestamp { get; set; }

    /// <inheritdoc />
    public ETag ETag { get; set; }

    /// <inheritdoc cref="KnxSourceAddress.IndividualAddress" />
    public string ia { get; init; } = default!;

    /// <inheritdoc cref="KnxGroupAddressParsed.GroupAddress" />
    public string ga { get; init; } = default!;

    /// <inheritdoc cref="KnxEvent.ValueAsString" />
    public string v { get; init; } = default!;

    /// <inheritdoc cref="KnxEvent.ValueLabel" />
    public string? s { get; init; }

    /// <summary>UTC timestamp of the last event for this group address.</summary>
    public DateTimeOffset? dt { get; init; }

    /// <summary>Running count of events for this group address.</summary>
    public long c { get; set; }

    /// <inheritdoc cref="RowKey" />
    [IgnoreDataMember]
    public string Id { get { return RowKey; } }

    /// <inheritdoc cref="RowKey" />
    [IgnoreDataMember]
    public string GroupAddressName { get { return RowKey; } }

    /// <inheritdoc cref="KnxEvent.TimestampUtc" />
    [IgnoreDataMember]
    public DateTime TimestampUtc => dt?.UtcDateTime ?? DateTime.MinValue;

    /// <summary>
    /// Generate a <see cref="TableEntity" /> from this <see cref="KnxSnapshotEntity" />.
    /// </summary>
    public TableEntity GetEntity()
    {
        var entity = new TableEntity(PartitionKey, RowKey)
            {
                { nameof(ia), ia },
                { nameof(ga), ga },
                { nameof(v), v },
                { nameof(s), s },
                { nameof(dt), dt },
                { nameof(c), c },
            };
        return entity;
    }
}
