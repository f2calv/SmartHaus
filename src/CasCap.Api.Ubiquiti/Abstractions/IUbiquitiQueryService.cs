namespace CasCap.Abstractions;

/// <summary>
/// Defines the public query operations exposed by the Ubiquiti UniFi Protect camera service.
/// </summary>
public interface IUbiquitiQueryService
{
    /// <summary>
    /// Retrieves a snapshot of recent camera activity including last event timestamps per <see cref="UbiquitiEventType"/>.
    /// </summary>
    Task<UbiquitiSnapshot> GetSnapshot();

    /// <summary>
    /// Sends a camera alert event (motion, smart detection, ring) to all configured event sinks.
    /// </summary>
    /// <param name="type"><inheritdoc cref="UbiquitiEventType" path="/summary"/></param>
    /// <param name="cameraId">Optional camera identifier that produced the event.</param>
    /// <param name="cameraName">Optional display name of the camera.</param>
    /// <param name="score">Optional smart detection confidence score (0.0–1.0).</param>
    Task SendAlert(UbiquitiEventType type, string? cameraId = null, string? cameraName = null, double? score = null);
}
