namespace CasCap.Abstractions;

/// <summary>Adds host-specific measurements to an agent run, such as GPU energy use.</summary>
/// <remarks>
/// Registered by the host application. <see cref="CasCap.Services.AgentCommsResponder"/> calls every
/// registered enricher around each run, and <see cref="CasCap.Services.CommsDebugNotifier"/> asks each one
/// for the lines it contributes to the reply footer and the monitor-group timeline.
/// </remarks>
public interface IAgentRunEnricher
{
    /// <summary>Captures state before the agent runs, for example a hardware snapshot.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>State passed back to <see cref="AfterRunAsync"/>, or <see langword="null"/>.</returns>
    Task<object?> BeforeRunAsync(CancellationToken cancellationToken);

    /// <summary>Records measurements on the completed run, typically in <see cref="AgentRunResult.AdditionalProperties"/>.</summary>
    /// <param name="result">The completed run.</param>
    /// <param name="state">The state returned by <see cref="BeforeRunAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AfterRunAsync(AgentRunResult result, object? state, CancellationToken cancellationToken);

    /// <summary>Formats an extra line for the reply's stats footer.</summary>
    /// <param name="result">The completed run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The line, or <see langword="null"/> to add nothing.</returns>
    Task<string?> FormatFooterLineAsync(AgentRunResult result, CancellationToken cancellationToken);

    /// <summary>Formats extra lines for a run or sub-agent step in the monitor-group timeline.</summary>
    /// <param name="result">The run or step.</param>
    /// <returns>The lines, which may be empty.</returns>
    IEnumerable<string> FormatDebugLines(AgentRunResult result);
}
