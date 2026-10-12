namespace CasCap.Services;

/// <summary>
/// Tracks shutters that were commanded to new targets. Slat commands are sent only once a shutter has
/// reached its position, because a slat command interrupts vertical travel.
/// </summary>
/// <param name="targets">The shutters and their targets.</param>
/// <param name="tolerancePct">Accepted deviation in percentage points; actuators often report 98 for closed.</param>
/// <param name="readShutters">Reads the current shutter states.</param>
/// <param name="moveShutter">Sends a shutter command without waiting for feedback.</param>
public sealed class KnxShutterMovement(
    IReadOnlyList<KnxShutterTarget> targets,
    double tolerancePct,
    Func<CancellationToken, Task<List<KnxShutter>>> readShutters,
    Func<KnxShutterStateChangeRequest, CancellationToken, Task> moveShutter)
{
    private readonly HashSet<string> _slatsCommanded = new(StringComparer.Ordinal);

    /// <summary>Gets the shutters and their targets.</summary>
    public IReadOnlyList<KnxShutterTarget> Targets => targets;

    /// <summary>
    /// Reads the current states, commands slats for shutters that have stopped at their position, and
    /// returns the shutters still short of their targets.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of shutters at their targets and the names of the rest.</returns>
    public async Task<(int Completed, List<string> Pending)> AdvanceAsync(CancellationToken cancellationToken)
    {
        var shutters = (await readShutters(cancellationToken).ConfigureAwait(false))
            .ToDictionary(shutter => shutter.GroupName, StringComparer.Ordinal);
        var completed = 0;
        var pending = new List<string>();
        foreach (var target in targets)
        {
            if (!shutters.TryGetValue(target.GroupName, out var shutter))
            {
                pending.Add(target.GroupName);
                continue;
            }

            var positioned = target.Position is null || IsAt(shutter.Position, target.Position.Value);
            var slatsDone = target.Slats is null || IsAt(shutter.SlatsPosition, target.Slats.Value);
            if (positioned && !slatsDone && _slatsCommanded.Add(target.GroupName))
                await moveShutter(new KnxShutterStateChangeRequest
                {
                    GroupName = target.GroupName,
                    Slats = target.Slats,
                }, cancellationToken).ConfigureAwait(false);

            if (positioned && slatsDone)
                completed++;
            else
                pending.Add(target.GroupName);
        }
        return (completed, pending);
    }

    /// <summary>Gets whether a reported percentage is within tolerance of the desired value.</summary>
    /// <param name="actual">The reported value, or <see langword="null"/> when unknown.</param>
    /// <param name="desired">The desired value.</param>
    /// <param name="tolerancePct">Accepted deviation in percentage points.</param>
    public static bool IsAt(double? actual, double desired, double tolerancePct)
        => actual is not null && Math.Abs(actual.Value - desired) <= tolerancePct;

    private bool IsAt(double? actual, double desired) => IsAt(actual, desired, tolerancePct);
}
