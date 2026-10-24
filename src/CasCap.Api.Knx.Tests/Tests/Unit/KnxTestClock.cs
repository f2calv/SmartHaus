using System.Threading.Channels;

namespace CasCap.Tests;

/// <summary>Steps the one-shot delays used by KNX automation without sleeping.</summary>
internal sealed class KnxTestClock : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<StepTimer> _timers = [];
    private readonly Channel<TimeSpan> _registrations = Channel.CreateUnbounded<TimeSpan>();
    private DateTimeOffset _now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
            return _now;
    }

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new StepTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>Waits until an expected delay has actually been registered.</summary>
    internal async Task WaitForDelayAsync(TimeSpan delay)
    {
        while (await _registrations.Reader.ReadAsync(TestContext.Current.CancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken) != delay) { }
    }

    /// <summary>Advances the clock and invokes due callbacks outside its lock.</summary>
    internal void Advance(TimeSpan amount)
    {
        List<StepTimer> due;
        lock (_gate)
        {
            _now += amount;
            due = [.. _timers.Where(timer => timer.Due <= _now)];
            foreach (var timer in due)
                _timers.Remove(timer);
        }
        foreach (var timer in due)
            timer.Fire();
    }

    private sealed class StepTimer(KnxTestClock clock, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset Due { get; private set; }
        private bool _disposed;

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (period != Timeout.InfiniteTimeSpan)
                throw new NotSupportedException("The KNX test clock supports only one-shot delays.");
            lock (clock._gate)
            {
                if (_disposed)
                    return false;
                clock._timers.Remove(this);
                if (dueTime != Timeout.InfiniteTimeSpan)
                {
                    Due = clock._now + dueTime;
                    clock._timers.Add(this);
                }
                clock._registrations.Writer.TryWrite(dueTime);
                return true;
            }
        }

        public void Fire() => callback(state);

        public void Dispose()
        {
            lock (clock._gate)
            {
                _disposed = true;
                clock._timers.Remove(this);
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
