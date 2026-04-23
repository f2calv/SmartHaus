namespace CasCap.Models;

/// <summary>
/// Azure Table Storage snapshot entity that stores the latest edge hardware reading.
/// Uses readable column names since this is a low-volume summary entity.
/// </summary>
public class EdgeHardwareSnapshotEntity : ITableEntity
{
    /// <summary>Parameterless constructor required by Azure Table Storage SDK.</summary>
    public EdgeHardwareSnapshotEntity() { }

    /// <summary>
    /// Creates a new snapshot entity from an <see cref="EdgeHardwareEvent"/>.
    /// </summary>
    public EdgeHardwareSnapshotEntity(string partitionKey, EdgeHardwareEvent evt)
    {
        PartitionKey = partitionKey;
        RowKey = evt.NodeName;

        NodeName = evt.NodeName;
        GpuPowerDrawW = evt.GpuPowerDrawW.HasValue ? Math.Round(evt.GpuPowerDrawW.Value, 1) : null;
        GpuTemperatureC = evt.GpuTemperatureC.HasValue ? Math.Round(evt.GpuTemperatureC.Value, 1) : null;
        GpuUtilizationPercent = evt.GpuUtilizationPercent.HasValue ? Math.Round(evt.GpuUtilizationPercent.Value, 1) : null;
        GpuMemoryUtilizationPercent = evt.GpuMemoryUtilizationPercent.HasValue ? Math.Round(evt.GpuMemoryUtilizationPercent.Value, 1) : null;
        GpuMemoryUsedMiB = evt.GpuMemoryUsedMiB.HasValue ? Math.Round(evt.GpuMemoryUsedMiB.Value, 1) : null;
        GpuMemoryTotalMiB = evt.GpuMemoryTotalMiB.HasValue ? Math.Round(evt.GpuMemoryTotalMiB.Value, 1) : null;
        CpuTemperatureC = evt.CpuTemperatureC.HasValue ? Math.Round(evt.CpuTemperatureC.Value, 1) : null;
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

    /// <inheritdoc cref="EdgeHardwareEvent.NodeName"/>
    public string? NodeName { get; init; }

    /// <inheritdoc cref="EdgeHardwareEvent.GpuPowerDrawW"/>
    public double? GpuPowerDrawW { get; init; }

    /// <inheritdoc cref="EdgeHardwareEvent.GpuTemperatureC"/>
    public double? GpuTemperatureC { get; init; }

    /// <inheritdoc cref="EdgeHardwareEvent.GpuUtilizationPercent"/>
    public double? GpuUtilizationPercent { get; init; }

    /// <inheritdoc cref="EdgeHardwareEvent.GpuMemoryUtilizationPercent"/>
    public double? GpuMemoryUtilizationPercent { get; init; }

    /// <inheritdoc cref="EdgeHardwareEvent.GpuMemoryUsedMiB"/>
    public double? GpuMemoryUsedMiB { get; init; }

    /// <inheritdoc cref="EdgeHardwareEvent.GpuMemoryTotalMiB"/>
    public double? GpuMemoryTotalMiB { get; init; }

    /// <inheritdoc cref="EdgeHardwareEvent.CpuTemperatureC"/>
    public double? CpuTemperatureC { get; init; }

    /// <summary>UTC timestamp of the last reading.</summary>
    public DateTimeOffset? ReadingUtc { get; init; }

    /// <summary>
    /// Generate a <see cref="TableEntity"/> from this <see cref="EdgeHardwareSnapshotEntity"/>.
    /// </summary>
    public TableEntity GetEntity()
    {
        var entity = new TableEntity(PartitionKey, RowKey)
        {
            { nameof(ReadingUtc), ReadingUtc },
        };
        if (NodeName is not null) entity[nameof(NodeName)] = NodeName;
        if (GpuPowerDrawW.HasValue) entity[nameof(GpuPowerDrawW)] = GpuPowerDrawW.Value;
        if (GpuTemperatureC.HasValue) entity[nameof(GpuTemperatureC)] = GpuTemperatureC.Value;
        if (GpuUtilizationPercent.HasValue) entity[nameof(GpuUtilizationPercent)] = GpuUtilizationPercent.Value;
        if (GpuMemoryUtilizationPercent.HasValue) entity[nameof(GpuMemoryUtilizationPercent)] = GpuMemoryUtilizationPercent.Value;
        if (GpuMemoryUsedMiB.HasValue) entity[nameof(GpuMemoryUsedMiB)] = GpuMemoryUsedMiB.Value;
        if (GpuMemoryTotalMiB.HasValue) entity[nameof(GpuMemoryTotalMiB)] = GpuMemoryTotalMiB.Value;
        if (CpuTemperatureC.HasValue) entity[nameof(CpuTemperatureC)] = CpuTemperatureC.Value;
        return entity;
    }
}
