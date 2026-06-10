namespace CasCap.Models;


/// <summary>Event arguments for motion detection events.</summary>
public sealed class MotionDetectedEventArgs
{
    /// <summary>Indicates whether motion has started.</summary>
    public bool MotionStarted { get; set; } = false;

    /// <summary>Indicates whether motion has stopped.</summary>
    public bool MotionStopped { get; set; } = false;
}
