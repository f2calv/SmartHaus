namespace CasCap.Extensions;

/// <summary>
/// LINQ ordering extensions for <see cref="FloorType"/> that sort floors
/// from top to bottom: DG → OG → EG → KG.
/// </summary>
public static class FloorTypeExtensions
{
    private static int FloorSortOrder(FloorType floor) => floor switch
    {
        FloorType.DG => 0,
        FloorType.OG => 1,
        FloorType.EG => 2,
        FloorType.KG => 3,
        _ => 4,
    };

    /// <summary>
    /// Orders a sequence of <see cref="FloorType"/> values from top to bottom.
    /// </summary>
    public static IOrderedEnumerable<FloorType> OrderByFloor(this IEnumerable<FloorType> source)
        => source.OrderBy(FloorSortOrder);

    /// <summary>
    /// Orders a sequence by a <see cref="FloorType"/> key selector from top to bottom.
    /// </summary>
    public static IOrderedEnumerable<T> OrderByFloor<T>(this IEnumerable<T> source, Func<T, FloorType> keySelector)
        => source.OrderBy(p => FloorSortOrder(keySelector(p)));
}
