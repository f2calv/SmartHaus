namespace CasCap.Services;

/// <summary>Reports shutter travel to the chat until every commanded shutter reaches its target.</summary>
/// <param name="description">User-facing description, for example <c>Closing 12 shutters</c>.</param>
/// <param name="movement">The commanded shutters.</param>
/// <param name="timeout">Time to wait before reporting the remaining shutters as unconfirmed.</param>
public sealed class ShutterMovementOperation(string description, KnxShutterMovement movement, TimeSpan timeout)
    : ITrackedOperation
{
    /// <inheritdoc/>
    public string Description => description;

    /// <inheritdoc/>
    public TimeSpan Timeout => timeout;

    /// <inheritdoc/>
    public async ValueTask<TrackedOperationProgress> PollAsync(CancellationToken cancellationToken)
    {
        var (completed, pending) = await movement.AdvanceAsync(cancellationToken).ConfigureAwait(false);
        return new TrackedOperationProgress(completed, movement.Targets.Count, pending);
    }
}
