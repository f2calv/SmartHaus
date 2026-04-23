namespace CasCap.Abstractions;

/// <summary>Motion detection device abstraction.</summary>
public interface IMotionDetectionDevice
{
    /// <summary>Raised when motion state changes.</summary>
    event EventHandler<MotionDetectedEventArgs> MotionDetectedEvent;

    /// <summary>Gets whether motion is currently detected.</summary>
    bool IsMotionDetected { get; }
}
