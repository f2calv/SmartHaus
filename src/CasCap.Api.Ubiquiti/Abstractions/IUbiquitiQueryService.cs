namespace CasCap.Abstractions;

/// <summary>
/// Defines the public query operations exposed by the Ubiquiti UniFi Protect camera service.
/// </summary>
public interface IUbiquitiQueryService
{
    /// <summary>
    /// Retrieves a snapshot of recent camera activity including last event timestamps per <see cref="UbiquitiEventType"/>.
    /// </summary>
    public Task<UbiquitiSnapshot> GetSnapshot();

    /// <summary>
    /// Sends a camera alert event (motion, smart detection, ring) to all configured event sinks.
    /// </summary>
    /// <param name="type"><inheritdoc cref="UbiquitiEventType" path="/summary"/></param>
    /// <param name="cameraId">Optional camera identifier that produced the event.</param>
    /// <param name="cameraName">Optional display name of the camera.</param>
    /// <param name="score">Optional smart detection confidence score (0.0–1.0).</param>
    /// <param name="webhook">Optional UniFi Protect payload containing trigger metadata and a thumbnail.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Acceptance or a payload validation error; sink failures propagate.</returns>
    public Task<UbiquitiWebhookResult> SendAlert(
        UbiquitiEventType type,
        string? cameraId = null,
        string? cameraName = null,
        double? score = null,
        UbiquitiWebhookRequest? webhook = null,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a person, vehicle, animal or package smart-detection alert.</summary>
    /// <param name="type">Case-insensitive smart detection type.</param>
    /// <param name="cameraId">Optional source camera identifier.</param>
    /// <param name="cameraName">Optional camera display name.</param>
    /// <param name="score">Optional confidence score.</param>
    /// <param name="webhook">Optional alarm metadata and thumbnail.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Acceptance or a detection/payload validation error; sink failures propagate.</returns>
    public Task<UbiquitiWebhookResult> SendSmartDetectionAlert(
        string type,
        string? cameraId = null,
        string? cameraName = null,
        double? score = null,
        UbiquitiWebhookRequest? webhook = null,
        CancellationToken cancellationToken = default);
}
