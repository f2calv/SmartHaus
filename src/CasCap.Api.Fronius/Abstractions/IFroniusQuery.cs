namespace CasCap.Abstractions;

/// <summary>
/// Provides query access to Fronius inverter snapshot and event data.
/// </summary>
public interface IFroniusQuery
{
    /// <summary>
    /// Retrieves the latest solar inverter snapshot.
    /// </summary>
    Task<InverterSnapshot> GetSnapshot();
}
