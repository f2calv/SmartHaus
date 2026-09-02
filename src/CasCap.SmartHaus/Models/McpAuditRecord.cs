namespace CasCap.Models;

/// <summary>
/// Immutable audit record emitted for every MCP tool invocation.
/// </summary>
/// <remarks>
/// Written to structured logs and optionally to a Redis Stream by <see cref="CasCap.Services.McpAuditMiddleware"/>.
/// </remarks>
public sealed record McpAuditRecord
{
    /// <summary>UTC timestamp when the tool invocation started.</summary>
    [Description("UTC timestamp of the invocation.")]
    public required DateTime TimestampUtc { get; init; }

    /// <summary>The tool name as registered in the MCP server (snake_case).</summary>
    [Description("MCP tool name.")]
    public required string ToolName { get; init; }

    /// <summary>Serialised arguments passed to the tool.</summary>
    [Description("JSON-encoded arguments dictionary.")]
    public required string Arguments { get; init; }

    /// <summary>Duration of the tool execution.</summary>
    [Description("Execution duration.")]
    public required TimeSpan Duration { get; init; }

    /// <summary>Whether the invocation completed successfully.</summary>
    [Description("True if the tool returned without throwing.")]
    public required bool Success { get; init; }

    /// <summary>Exception type name if the invocation failed; otherwise <c>null</c>.</summary>
    [Description("Exception type if failed.")]
    public string? ErrorType { get; init; }

    /// <summary>Exception message if the invocation failed; otherwise <c>null</c>.</summary>
    [Description("Error message if failed.")]
    public string? ErrorMessage { get; init; }

    /// <summary>Whether human approval was required for this invocation.</summary>
    [Description("True if the tool required human approval.")]
    public bool ApprovalRequired { get; init; }

    /// <summary>Whether approval was granted (null if not required).</summary>
    [Description("True if approved, false if denied, null if not required.")]
    public bool? ApprovalGranted { get; init; }
}
