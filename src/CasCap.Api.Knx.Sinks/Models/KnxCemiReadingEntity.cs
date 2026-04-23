namespace CasCap.Models;

/// <summary>
/// Azure Table entity that records the raw serialised CEMI frame for a KNX telegram.
/// </summary>
public class KnxCemiReadingEntity : ITableEntity
{
    /// <inheritdoc/>
    public KnxCemiReadingEntity() { }

    /// <summary>
    /// Initializes a new instance from a <see cref="KnxEvent"/> and its serialised CEMI hex string.
    /// </summary>
    /// <param name="partitionKey"></param>
    /// <param name="knxEvent">The decoded telegram.</param>
    /// <param name="cemiHex">The CEMI frame as a hex-encoded string.</param>
    public KnxCemiReadingEntity(string partitionKey, KnxEvent knxEvent, string cemiHex)
    {
        PartitionKey = partitionKey;
        RowKey = knxEvent.TimestampUtc.Ticks.ToString();
        CemiHex = cemiHex;
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

    /// <summary>
    /// The serialised CEMI frame as a hex-encoded string.
    /// </summary>
    public string CemiHex { get; init; } = default!;

    /// <summary>
    /// Computed UTC <see cref="DateTime"/> from <see cref="RowKey"/>.
    /// </summary>
    [IgnoreDataMember]
    public DateTime TimestampUtc => new(long.Parse(RowKey), DateTimeKind.Utc);

    /// <summary>
    /// Generate a <see cref="TableEntity"/> from this <see cref="KnxCemiReadingEntity"/>.
    /// </summary>
    public TableEntity GetEntity()
    {
        var entity = new TableEntity(PartitionKey, RowKey)
        {
            { nameof(CemiHex), CemiHex },
        };
        return entity;
    }
}
