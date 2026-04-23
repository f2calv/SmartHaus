namespace CasCap.Models;

/// <summary>
/// Maps a KM200 datapoint ID to an Azure Table Storage column name and optional OpenTelemetry metric definition.
/// </summary>
public record DatapointMapping
{
    /// <summary>
    /// The Azure Table Storage column name used in the snapshot table.
    /// Must match the corresponding <see cref="BuderusSnapshot"/> property name.
    /// </summary>
    [Required, MinLength(1)]
    public required string ColumnName { get; init; }

    /// <summary>
    /// The OpenTelemetry gauge metric name (e.g. <c>"haus.hvac.temperature"</c>).
    /// When <see langword="null"/>, no metric is emitted for this datapoint.
    /// </summary>
    public string? MetricName { get; init; }

    /// <summary>
    /// The unit for the OpenTelemetry metric (e.g. <c>"Cel"</c> for Celsius).
    /// </summary>
    public string? MetricUnit { get; init; }

    /// <summary>
    /// A human-readable description for the OpenTelemetry metric.
    /// </summary>
    public string? MetricDescription { get; init; }
}
