namespace CasCap.Models;

/// <summary>
/// This object is a simplified representation of a KNX Area/Line combination,
/// the fully fledged object is the <see cref="Knx.Falcon.Sdk.KnxBus"/> object.
/// </summary>
public record KnxAreaLine
{
    /// <summary>The KNX area address (top-level segment).</summary>
    public byte Area { get; init; }

    /// <summary>The KNX line address within the area.</summary>
    public byte Line { get; init; }

    //public override bool Equals(object obj) => obj is KnxAreaLine line && Area == line.Area && Line == line.Line;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Area, Line);

    /// <inheritdoc/>
    public override string ToString() => $"Area {Area}, Line {Line}";
}
