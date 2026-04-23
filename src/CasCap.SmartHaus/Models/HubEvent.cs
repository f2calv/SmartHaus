namespace CasCap.Models;

/// <summary>
/// Represents an event received by <see cref="CasCap.Hubs.HausHub"/> from a connected feature pod.
/// Used by hub-side event sinks to track message flow and publish telemetry.
/// </summary>
/// <param name="EventType">
/// The name of the event type (e.g. <c>"FroniusEvent"</c>, <c>"KnxEvent"</c>).
/// </param>
/// <param name="Timestamp">UTC timestamp at which the hub received the event.</param>
public record HubEvent(string EventType, DateTimeOffset Timestamp);
