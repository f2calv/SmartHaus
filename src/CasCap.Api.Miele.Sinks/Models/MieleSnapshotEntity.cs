namespace CasCap.Models;

/// <summary>Azure Table Storage snapshot entity storing the latest Miele appliance state.</summary>
public class MieleSnapshotEntity : ITableEntity
{
    /// <summary>Parameterless constructor required by Azure Table Storage SDK.</summary>
    public MieleSnapshotEntity() { }

    /// <summary>Initializes a new instance from a <see cref="MieleEvent"/>.</summary>
    public MieleSnapshotEntity(string partitionKey, MieleEvent evt)
    {
        PartitionKey = partitionKey;
        RowKey = evt.DeviceId;

        DeviceId = evt.DeviceId;
        DeviceName = evt.DeviceName;
        StatusCode = evt.StatusCode;
        ProgramId = evt.ProgramId;
        ErrorCode = evt.ErrorCode;
        ReadingUtc = new DateTimeOffset(evt.TimestampUtc, TimeSpan.Zero);
    }

    /// <inheritdoc />
    public string PartitionKey { get; set; } = default!;

    /// <inheritdoc />
    public string RowKey { get; set; } = default!;

    /// <inheritdoc />
    public DateTimeOffset? Timestamp { get; set; }

    /// <inheritdoc />
    public ETag ETag { get; set; }

    /// <summary>Device identifier.</summary>
    public string DeviceId { get; init; } = default!;

    /// <summary>Appliance name.</summary>
    public string? DeviceName { get; init; }

    /// <summary>Status code.</summary>
    public int? StatusCode { get; init; }

    /// <summary>Program ID.</summary>
    public int? ProgramId { get; init; }

    /// <summary>Error code.</summary>
    public int? ErrorCode { get; init; }

    /// <summary>UTC timestamp of the last reading.</summary>
    public DateTimeOffset? ReadingUtc { get; init; }

    /// <summary>Generates a <see cref="TableEntity"/> from this instance.</summary>
    public TableEntity GetEntity()
    {
        var entity = new TableEntity(PartitionKey, RowKey)
        {
            { nameof(DeviceId), DeviceId },
            { nameof(ReadingUtc), ReadingUtc },
        };
        if (DeviceName is not null) entity.Add(nameof(DeviceName), DeviceName);
        if (StatusCode is not null) entity.Add(nameof(StatusCode), StatusCode);
        if (ProgramId is not null) entity.Add(nameof(ProgramId), ProgramId);
        if (ErrorCode is not null) entity.Add(nameof(ErrorCode), ErrorCode);
        return entity;
    }
}
