namespace CasCap.Models;

/// <summary>Represents a sensor reading event from a Buderus KM200 device.</summary>
public record BuderusEvent
{
    /// <summary>Initializes a new instance of the <see cref="BuderusEvent"/> class.</summary>
    /// <param name="id">KM200 datapoint path.</param>
    /// <param name="value">Sensor reading value.</param>
    /// <param name="timestampUtc">UTC timestamp of the reading.</param>
    [SetsRequiredMembers]
    public BuderusEvent(string id, object value, DateTime timestampUtc)
    {
        ArgumentNullException.ThrowIfNull(value);
        Id = id;
        Value = value.ToString()!;
        TimestampUtc = timestampUtc;
        try
        {
            var cleaned = id.Substring(1).Replace("/", "_");
            type = cleaned.ParseEnum<BuderusGauges>();
        }
        catch
        {
            //swallow exception
        }
    }

    /// <summary>
    /// e.g. /dhwCircuits/dhw1/extraDhw/stopTemp
    /// </summary>
    [Description("KM200 datapoint path (e.g. /dhwCircuits/dhw1/extraDhw/stopTemp).")]
    public required string Id { get; init; }

    /// <summary>
    /// e.g. 19
    /// </summary>
    [Description("Sensor reading value as a string (e.g. '19').")]
    public required string Value { get; init; }

    /// <summary>Gauge type parsed from the datapoint path.</summary>
    [Description("Gauge type parsed from the datapoint path.")]
    public BuderusGauges type { get; init; } = BuderusGauges.Unknown;

    /// <summary>UTC timestamp of the reading.</summary>
    [Description("UTC timestamp of the reading.")]
    public required DateTime TimestampUtc { get; init; }

    /// <summary>Returns a string representation of the event.</summary>
    /// <returns>A formatted string containing timestamp, type, and value.</returns>
    public override string ToString() => $"{TimestampUtc:yyyy-MM-dd HH:mm}, {type}, {Value}";
}
