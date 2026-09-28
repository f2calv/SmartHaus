namespace CasCap.Services;

/// <summary><see cref="ICommsEventFormatter"/> that prefixes the message with its time, environment and source.</summary>
/// <remarks>
/// Produces <c>HH:mm:ss.fff UTC [environment] [source] message</c>, using invariant culture. Register it before
/// <c>AddComms</c> to replace the <see cref="PlainCommsEventFormatter"/> default.
/// </remarks>
public sealed class TimestampedCommsEventFormatter : ICommsEventFormatter
{
    /// <inheritdoc/>
    public string Format(CommsEvent commsEvent)
    {
        var ts = commsEvent.TimestampUtc.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        return $"{ts} UTC [{commsEvent.Environment}] [{commsEvent.Source}] {commsEvent.Message}";
    }
}
