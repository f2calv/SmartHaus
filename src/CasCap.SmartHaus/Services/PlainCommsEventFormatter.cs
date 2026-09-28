namespace CasCap.Services;

/// <summary>Default <see cref="ICommsEventFormatter"/> that forwards the event message unchanged.</summary>
public sealed class PlainCommsEventFormatter : ICommsEventFormatter
{
    /// <inheritdoc/>
    public string Format(CommsEvent commsEvent) => commsEvent.Message;
}
