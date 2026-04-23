namespace CasCap.Models;

/// <summary>
/// Azure Table Storage entity that records every individual DoorBird event.
/// </summary>
public class DoorBirdReadingEntity : ITableEntity
{
    /// <summary>
    /// Parameterless constructor required by Azure Table Storage deserialization.
    /// </summary>
    public DoorBirdReadingEntity() { }

    /// <summary>
    /// Creates a new reading entity from a <see cref="DoorBirdEvent"/>.
    /// </summary>
    public DoorBirdReadingEntity(DoorBirdEvent evt)
    {
        PartitionKey = evt.DoorBirdEventType.ToString();
        RowKey = evt.DateCreatedUtc.Ticks.ToString();
        t = evt.DoorBirdEventType.ToString();
        i = evt.bytes is not null;
    }

    /// <inheritdoc />
    public string PartitionKey { get; set; } = default!;

    /// <inheritdoc />
    public string RowKey { get; set; } = default!;

    /// <inheritdoc />
    public DateTimeOffset? Timestamp { get; set; }

    /// <inheritdoc />
    public ETag ETag { get; set; }

    /// <inheritdoc cref="EventType"/>
    public string t { get; init; } = default!;

    /// <inheritdoc cref="HasImage"/>
    public bool i { get; init; }

    /// <summary>
    /// The string representation of the <see cref="DoorBirdEventType"/>.
    /// </summary>
    public string EventType => t;

    /// <summary>
    /// Whether an image was captured for this event.
    /// </summary>
    public bool HasImage => i;

    /// <summary>
    /// The UTC date/time reconstructed from the <see cref="RowKey"/> ticks.
    /// </summary>
    public DateTime TimestampUtc => new(long.Parse(RowKey), DateTimeKind.Utc);

    /// <summary>
    /// Generates a <see cref="TableEntity"/> from this <see cref="DoorBirdReadingEntity"/>.
    /// </summary>
    public TableEntity GetEntity() => new(PartitionKey, RowKey)
    {
        { nameof(t), t },
        { nameof(i), i },
    };
}
