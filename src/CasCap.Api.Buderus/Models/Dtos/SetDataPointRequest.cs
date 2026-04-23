namespace CasCap.Models;

/// <summary>
/// Request body for updating a writeable KM200 datapoint value.
/// </summary>
public record SetDataPointRequest
{
    /// <summary>
    /// The new value to write to the datapoint.
    /// For <see cref="MyDatapointType.floatValue"/> datapoints, supply a numeric string (e.g. <c>"55.0"</c>).
    /// For <see cref="MyDatapointType.stringValue"/> datapoints, supply the option string (e.g. <c>"Always_On"</c>).
    /// </summary>
    [Required]
    public required string Value { get; init; }
}
