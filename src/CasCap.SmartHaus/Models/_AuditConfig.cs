namespace CasCap.Models;

/// <summary>
/// Configuration for the MCP tool call auditing and human-in-the-loop approval gate.
/// </summary>
/// <remarks>
/// Bound from the <c>CasCap:AuditConfig</c> section in <c>appsettings.json</c>.
/// Controls both the audit logging sink and the approval workflow behaviour.
/// </remarks>
public record AuditConfig : IAppConfig
{
    /// <inheritdoc/>
    public static string ConfigurationSectionName => $"{nameof(CasCap)}:{nameof(AuditConfig)}";

    /// <summary>Whether MCP tool call auditing is enabled.</summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool Enabled { get; init; } = true;

    /// <summary>Redis Stream key for durable audit history.</summary>
    /// <remarks>Defaults to <c>"audit:mcp:calls"</c>.</remarks>
    [Required, MinLength(1)]
    public string RedisStreamKey { get; init; } = "audit:mcp:calls";

    /// <summary>Maximum number of entries retained in the Redis audit stream (MAXLEN).</summary>
    /// <remarks>Defaults to <c>10000</c>. Set to <c>0</c> to disable trimming.</remarks>
    [Range(0, int.MaxValue)]
    public int MaxStreamLength { get; init; } = 10_000;

    /// <summary>Whether the human-in-the-loop approval gate is enabled.</summary>
    /// <remarks>Defaults to <c>true</c>.</remarks>
    public bool ApprovalEnabled { get; init; } = true;

    /// <summary>Timeout in milliseconds for waiting for an approval response.</summary>
    /// <remarks>Defaults to <c>60000</c> (60 seconds). Used by <see cref="CasCap.Services.McpAuditMiddleware"/>.</remarks>
    [Range(1000, 300_000)]
    public int ApprovalTimeoutMs { get; init; } = 60_000;

    /// <summary>
    /// Tool names that always require approval regardless of the <see cref="CasCap.Attributes.RequiresApprovalAttribute"/>.
    /// </summary>
    /// <remarks>
    /// Matching is case-insensitive — use either PascalCase method names (e.g. <c>UnlockHouseDoor</c>)
    /// or snake_case registry names (e.g. <c>unlock_house_door</c>).
    /// Config-driven override for operational control without redeployment.
    /// </remarks>
    public string[] AlwaysRequireApproval { get; init; } = [];

    /// <summary>
    /// Tool names that are exempt from approval even if decorated with <see cref="CasCap.Attributes.RequiresApprovalAttribute"/>.
    /// </summary>
    /// <remarks>
    /// Matching is case-insensitive — use either PascalCase method names or snake_case registry names.
    /// Useful for development environments or trusted automation scenarios.
    /// </remarks>
    public string[] NeverRequireApproval { get; init; } = [];
}
