namespace CasCap.Services;

/// <summary>Supervises KNX children and starts host shutdown before awaiting failed-run cleanup.</summary>
public sealed class KnxWorkerLifetime(
    KnxConnectionHealthCheck health,
    IHostApplicationLifetime applicationLifetime)
{
    /// <summary>Gets the readiness state shared by the owned connection and sender.</summary>
    public KnxConnectionHealthCheck Health => health;

    /// <summary>Withdraws readiness and starts the host's shutdown budget after a fatal failure.</summary>
    public void StopApplication()
    {
        health.ConnectionActive = false;
        health.IsLeader = false;
        applicationLifetime.StopApplication();
    }

    /// <summary>Runs required children, cancelling and joining siblings after any child exits.</summary>
    public Task RunAsync(Func<CancellationToken, Task>[] workers, CancellationToken cancellationToken)
        => Parallel.ForEachAsync(workers,
            new ParallelOptions { MaxDegreeOfParallelism = workers.Length, CancellationToken = cancellationToken },
            async (worker, token) =>
            {
                try
                {
                    await worker(token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    throw new InvalidOperationException("A KNX ownership worker stopped unexpectedly.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
                {
                    StopApplication();
                    throw;
                }
            });
}
