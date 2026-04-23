namespace CasCap.Models;

/// <summary>
/// The type of DoorBird event: doorbell, motionsensor, rfid, or door relay.
/// </summary>
public enum DoorBirdEventType
{
    /// <summary>
    /// Fired when the physical doorbell button on the DoorBird unit is pressed.
    /// Recorded as a monotonically increasing count since the service started.
    /// </summary>
    [Metric("doorbird.doorbell.count", "{event}", Description = "Total doorbell ring events since startup")]
    Doorbell,

    /// <summary>
    /// Fired when the built-in passive-infrared motion sensor detects movement in the
    /// camera's field of view. Recorded as a monotonically increasing count since the
    /// service started.
    /// </summary>
    [Metric("doorbird.motion.count", "{event}", Description = "Total motion sensor events since startup")]
    MotionSensor,

    /// <summary>
    /// Fired when a registered RFID transponder (key-fob, card, etc.) is presented to the
    /// DoorBird's reader. Recorded as a monotonically increasing count since the service started.
    /// </summary>
    [Metric("doorbird.rfid.count", "{event}", Description = "Total RFID events since startup")]
    Rfid,

    /// <summary>
    /// Fired when the door-lock relay is activated to electrically release the strike plate
    /// and grant physical access. Recorded as a monotonically increasing count since the
    /// service started.
    /// </summary>
    [Metric("doorbird.relay.count", "{event}", Description = "Total door relay trigger events since startup")]
    DoorRelay,
}
