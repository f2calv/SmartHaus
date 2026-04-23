namespace CasCap.Abstractions;

/// <summary>
/// Provides query access to Buderus heat pump snapshot and event data.
/// </summary>
public interface IBuderusQuery
{
    /// <summary>
    /// Retrieves a typed snapshot of key Buderus heat pump sensor values.
    /// </summary>
    Task<BuderusSnapshot> GetSnapshot();
}
