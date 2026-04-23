namespace CasCap.Abstractions;

/// <summary>
/// Provides query access to Sicce water pump snapshot data.
/// </summary>
public interface ISicceQuery
{
    /// <summary>
    /// Retrieves the latest Sicce device snapshot.
    /// </summary>
    Task<SicceSnapshot> GetSnapshot();
}
