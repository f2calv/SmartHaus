namespace CasCap.Models.Dtos;

/// <summary>
/// Represents a distinct room in the house, associated with a floor.
/// </summary>
public record KnxRoom
{
    /// <inheritdoc cref="FloorType" path="/summary"/>
    [Description("Building floor abbreviation. Values: KG (basement), EG (ground floor), OG (upper floor), DG (top).")]
    public required FloorType Floor { get; init; }

    /// <inheritdoc cref="RoomType" path="/summary"/>
    [Description("Room name (e.g. Kitchen, Office, LivingRoom).")]
    public RoomType? Room { get; init; }
}
