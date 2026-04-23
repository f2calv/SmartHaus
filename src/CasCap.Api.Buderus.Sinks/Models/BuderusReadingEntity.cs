namespace CasCap.Models;

/// <summary>
/// Azure Table Storage line-item entity that records a single <see cref="BuderusEvent.Value"/>
/// for each <see cref="BuderusEvent.Id"/> and <see cref="BuderusEvent.TimestampUtc"/>.
/// Uses <c>yyMMdd</c> date-based partitioning consistent with all other domain sinks.
/// </summary>
public class BuderusReadingEntity : ITableEntity
{
    /// <summary>Initializes a new instance of the <see cref="BuderusReadingEntity"/> class.</summary>
    public BuderusReadingEntity() { }

    /// <summary>Initializes a new instance of the <see cref="BuderusReadingEntity"/> class.</summary>
    /// <param name="evt">Buderus event to create entity from.</param>
    public BuderusReadingEntity(BuderusEvent evt)
    {
        this.be = evt;
        PartitionKey = evt.Id.Replace('/', '_');
        RowKey = evt.TimestampUtc.Ticks.ToString();
        d = evt.Id.Replace('/', '_');
        v = evt.Value;
    }

    /// <summary>Buderus event source.</summary>
    [IgnoreDataMember]
    public BuderusEvent be { get; init; } = default!;

    /// <inheritdoc />
    public string PartitionKey { get; set; } = default!;

    /// <inheritdoc />
    public string RowKey { get; set; } = default!;

    /// <inheritdoc />
    public DateTimeOffset? Timestamp { get; set; }

    /// <inheritdoc />
    public ETag ETag { get; set; }

    /// <summary>Datapoint identifier (slash-delimited path with slashes replaced by underscores).</summary>
    public string d { get; init; } = default!;

    /// <inheritdoc cref="BuderusEvent.Value" />
    public string v { get; init; } = default!;

    /// <inheritdoc cref="v" />
    [IgnoreDataMember]
    public string value => v;

    /// <summary>Datapoint path restored from <see cref="d"/> (underscores replaced with slashes).</summary>
    [IgnoreDataMember]
    public string Id => d.Replace('_', '/');

    /// <summary>UTC timestamp reconstructed from <see cref="RowKey"/>.</summary>
    [IgnoreDataMember]
    public DateTime TimestampUtc => new(long.Parse(RowKey), DateTimeKind.Utc);

    /// <summary>
    /// Generate a <see cref="TableEntity" /> from this <see cref="BuderusReadingEntity" />.
    /// </summary>
    public TableEntity GetEntity()
    {
        var entity = new TableEntity(PartitionKey, RowKey)
        {
            { nameof(d), d },
            { nameof(v), v },
        };
        return entity;
    }
}
