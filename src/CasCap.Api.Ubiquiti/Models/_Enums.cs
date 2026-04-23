namespace CasCap.Models;

/// <summary>
/// The type of Ubiquiti camera event: motion detection, smart detection, or connection state change.
/// </summary>
public enum UbiquitiEventType
{
    /// <summary>
    /// Fired when any camera detects motion in its field of view.
    /// Recorded as a monotonically increasing count since the service started.
    /// </summary>
    [Metric("ubiquiti.motion.count", "{event}", Description = "Total motion detection events since startup")]
    Motion,

    /// <summary>
    /// Fired when a camera's smart detection identifies a person in the field of view.
    /// </summary>
    [Metric("ubiquiti.smart.person.count", "{event}", Description = "Total smart detection person events since startup")]
    SmartDetectPerson,

    /// <summary>
    /// Fired when a camera's smart detection identifies a vehicle in the field of view.
    /// </summary>
    [Metric("ubiquiti.smart.vehicle.count", "{event}", Description = "Total smart detection vehicle events since startup")]
    SmartDetectVehicle,

    /// <summary>
    /// Fired when a camera's smart detection identifies an animal in the field of view.
    /// </summary>
    [Metric("ubiquiti.smart.animal.count", "{event}", Description = "Total smart detection animal events since startup")]
    SmartDetectAnimal,

    /// <summary>
    /// Fired when a camera's smart detection identifies a package in the field of view.
    /// </summary>
    [Metric("ubiquiti.smart.package.count", "{event}", Description = "Total smart detection package events since startup")]
    SmartDetectPackage,

    /// <summary>
    /// Fired when the doorbell ring button is pressed on a UniFi Protect doorbell camera.
    /// </summary>
    [Metric("ubiquiti.ring.count", "{event}", Description = "Total doorbell ring events since startup")]
    Ring,
}
