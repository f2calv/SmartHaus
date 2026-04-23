namespace CasCap.Abstractions;

/// <summary>
/// Defines the public query and control operations exposed by the Wiz smart lighting service.
/// </summary>
public interface IWizQueryService
{
    /// <summary>
    /// Returns all Wiz bulbs currently discovered on the local network.
    /// </summary>
    IReadOnlyDictionary<string, WizBulb> GetDiscoveredBulbs();

    /// <summary>
    /// Triggers an on-demand UDP broadcast discovery and returns all responding bulbs.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyDictionary<string, WizBulb>> DiscoverBulbs(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current pilot state (on/off, brightness, colour, scene) of a single bulb.
    /// </summary>
    /// <param name="bulbIdentifier">Device name, MAC address, or IP address of the target bulb.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<WizPilotState?> GetPilot(string bulbIdentifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the system configuration (firmware, MAC, module name) of a single bulb.
    /// </summary>
    /// <param name="bulbIdentifier">Device name, MAC address, or IP address of the target bulb.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<WizSystemConfig?> GetSystemConfig(string bulbIdentifier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the pilot state (on/off, brightness, colour, temperature, scene) of a single bulb.
    /// </summary>
    /// <param name="bulbIdentifier">Device name, MAC address, or IP address of the target bulb.</param>
    /// <param name="request">The desired state to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> SetPilot(string bulbIdentifier, WizSetPilotRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns a bulb on or off.
    /// </summary>
    /// <param name="bulbIdentifier">Device name, MAC address, or IP address of the target bulb.</param>
    /// <param name="on">True to turn on, false to turn off.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> SetPowerState(string bulbIdentifier, bool on, CancellationToken cancellationToken = default);
}
