namespace CasCap.Abstractions;

/// <summary>
/// Provides query access to edge hardware snapshot data from persistent storage.
/// </summary>
public interface IEdgeHardwareQuery
{
    /// <summary>
    /// Retrieves the latest edge hardware snapshot for every known node.
    /// </summary>
    Task<List<EdgeHardwareSnapshot>> GetSnapshots();
}
