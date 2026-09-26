namespace CasCap.Models;

/// <summary>The result of creating a poll through the messaging MCP tools.</summary>
public sealed record PollCreatedResult
{
    /// <summary>The poll identifier. Use this value to close the poll or read its status.</summary>
    [Description("Identifier of the created poll. Use this value to close the poll or read its status.")]
    public required string PollId { get; init; }
}
