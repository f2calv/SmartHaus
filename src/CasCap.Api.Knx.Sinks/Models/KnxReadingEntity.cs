namespace CasCap.Models;

/// <summary>
/// Azure Table Storage entity that records every individual KNX telegram.
/// Uses ultra-short column names to reduce payload size for high-volume data.
/// </summary>
public class KnxReadingEntity : ITableEntity
{
    /// <inheritdoc/>
    public KnxReadingEntity() { }

    /// <inheritdoc/>
    public KnxReadingEntity(KnxEvent evt)
    {
        PartitionKey = evt.Kga.Name;
        RowKey = evt.TimestampUtc.Ticks.ToString();
        ia = evt.Args.SourceAddress.IndividualAddress;
        ga = evt.Kga.GroupAddress;
        v = evt.ValueAsString;
        s = evt.ValueLabel;
    }

    /// <inheritdoc cref="KnxGroupAddressParsed.Name"/>
    public string PartitionKey { get; set; } = default!;

    /// <summary><inheritdoc cref="KnxEvent.TimestampUtc"/></summary>
    /// <remarks>
    /// DateTimeUtc.Ticks e.g. 638249938099545010
    /// </remarks>
    public string RowKey { get; set; } = default!;

    /// <inheritdoc/>
    public DateTimeOffset? Timestamp { get; set; }

    /// <inheritdoc/>
    public ETag ETag { get; set; }

    /// <inheritdoc cref="KnxSourceAddress.IndividualAddress"/>
    public string ia { get; init; } = default!;

    /// <inheritdoc cref="KnxGroupAddressParsed.GroupAddress"/>
    public string ga { get; init; } = default!;

    /// <inheritdoc cref="KnxEvent.ValueAsString"/>
    public string v { get; init; } = default!;

    /// <inheritdoc cref="KnxEvent.ValueLabel"/>
    public string? s { get; init; }

    // ── Full-name accessors ──────────────────────────────────────────

    /// <summary>KNX individual address of the sending device.</summary>
    [IgnoreDataMember]
    public string IndividualAddress => ia;

    /// <summary>KNX group address targeted by the telegram.</summary>
    [IgnoreDataMember]
    public string GroupAddress => ga;

    /// <summary>Decoded value of the telegram as a string.</summary>
    [IgnoreDataMember]
    public string ValueAsString => v;

    /// <summary>Human-readable label for the decoded value.</summary>
    [IgnoreDataMember]
    public string? ValueLabel => s;

    /// <summary>UTC timestamp derived from the <see cref="ITableEntity.RowKey"/> ticks value.</summary>
    [IgnoreDataMember]
    public DateTime TimestampUtc => new(long.Parse(RowKey), DateTimeKind.Utc);

    /// <summary>
    /// Generate a <see cref="TableEntity" /> from this <see cref="KnxReadingEntity" />.
    /// </summary>
    public TableEntity GetEntity()
    {
        var entity = new TableEntity(this.PartitionKey, this.RowKey)
            {
                { nameof(ia), ia },
                { nameof(ga), ga },
                { nameof(v), v },
                { nameof(s), s },
            };
        return entity;
    }
}
