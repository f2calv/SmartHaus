namespace CasCap.Abstractions;

/// <summary>
/// Provides query access to DoorBird activity snapshot data.
/// </summary>
public interface IDoorBirdQuery
{
    /// <summary>
    /// Retrieves a snapshot of recent DoorBird device activity.
    /// </summary>
    Task<DoorBirdSnapshot> GetSnapshot();
}
