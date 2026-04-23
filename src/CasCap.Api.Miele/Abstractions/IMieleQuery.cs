namespace CasCap.Abstractions;

/// <summary>Query interface implemented by Miele sink services that can return persisted data.</summary>
public interface IMieleQuery
{
    /// <summary>Retrieves the latest snapshot per appliance.</summary>
    Task<List<MieleSnapshot>> GetSnapshots();
}
