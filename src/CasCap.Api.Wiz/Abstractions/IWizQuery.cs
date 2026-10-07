namespace CasCap.Abstractions;

/// <summary>Query interface implemented by Wiz sink services that can return persisted data.</summary>
public interface IWizQuery
{
    /// <summary>Retrieves the latest snapshot per discovered bulb.</summary>
    public Task<List<WizSnapshot>> GetSnapshots();
}
