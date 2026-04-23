namespace CasCap.Models;

/// <summary>
/// Represents a single KM200 datapoint returned by the Buderus REST API.
/// Value-bearing types (<see cref="MyDatapointType.floatValue"/>, <see cref="MyDatapointType.stringValue"/>)
/// include <see cref="Value"/> and <see cref="UnitOfMeasure"/>; container types
/// (<see cref="MyDatapointType.refEnum"/>) carry only <see cref="References"/>.
/// </summary>
public record Km200DatapointObject
{
    /// <summary>
    /// Absolute datapoint path, e.g. <c>/dhwCircuits/dhw1/actualTemp</c>.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <inheritdoc cref="MyDatapointType" path="/summary"/>
    [JsonPropertyName("type")]
    public MyDatapointType Type { get; init; }

    /// <summary>
    /// Child datapoint references for <see cref="MyDatapointType.refEnum"/> container nodes.
    /// </summary>
    [JsonPropertyName("references")]
    public List<Km200DatapointReference>? References { get; init; }

    /// <summary>
    /// Whether the datapoint can be written to (<c>1</c>) or is read-only (<c>0</c>).
    /// </summary>
    [JsonPropertyName("writeable")]
    public int Writeable { get; init; }

    /// <summary>
    /// Whether the datapoint supports historical recording (<c>1</c>) or not (<c>0</c>).
    /// </summary>
    [JsonPropertyName("recordable")]
    public int Recordable { get; init; }

    /// <summary>
    /// Current value of the datapoint, or <see langword="null"/> for container types.
    /// </summary>
    [JsonPropertyName("value")]
    public object? Value { get; init; }

    /// <summary>
    /// Unit of measure string (e.g. <c>°C</c>, <c>l/min</c>), or <see langword="null"/> for container types.
    /// </summary>
    [JsonPropertyName("unitOfMeasure")]
    public string? UnitOfMeasure { get; init; }

    /// <inheritdoc/>
    public override string ToString() => $"{Id} {Type} {Value}";
}
