namespace CasCap.Abstractions;

/// <summary>
/// Provides query access to Shelly smart plug snapshot and event data.
/// </summary>
public interface IShellyQuery
{
    /// <summary>
    /// Retrieves snapshots for all known devices.
    /// </summary>
    Task<List<ShellySnapshot>> GetSnapshots();
}
