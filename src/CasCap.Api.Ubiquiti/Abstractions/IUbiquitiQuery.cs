namespace CasCap.Abstractions;

/// <summary>
/// Provides query access to Ubiquiti camera activity snapshot data.
/// </summary>
public interface IUbiquitiQuery
{
    /// <summary>
    /// Retrieves a snapshot of recent Ubiquiti camera activity.
    /// </summary>
    Task<UbiquitiSnapshot> GetSnapshot();
}
