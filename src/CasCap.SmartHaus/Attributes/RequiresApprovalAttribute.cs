namespace CasCap.Attributes;

/// <summary>
/// Marks an MCP tool method as requiring explicit human approval before execution.
/// </summary>
/// <remarks>
/// When applied to a method decorated with <see cref="ModelContextProtocol.Server.McpServerToolAttribute"/>,
/// the <see cref="CasCap.Services.McpAuditMiddleware"/> will gate execution behind a Signal poll
/// (or console confirmation in development) before invoking the tool.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresApprovalAttribute : Attribute
{
    /// <summary>Human-readable reason displayed in the approval prompt.</summary>
    public string? Reason { get; init; }
}
