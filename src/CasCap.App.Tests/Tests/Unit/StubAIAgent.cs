using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace CasCap.Tests.Unit;

/// <summary>
/// An <see cref="AIAgent"/> that refuses every operation, so the comms service treats the agent as
/// configured without any inference taking place.
/// </summary>
/// <remarks>
/// The reply drain loop wraps agent execution in its own error handling, so a refusal exercises the
/// queue end to end and stops short of a group reply.
/// </remarks>
public sealed class StubAIAgent : AIAgent
{
    private static NotSupportedException Refused() => new("stub agent performs no inference");

    /// <inheritdoc/>
    protected override ValueTask<AgentSession> CreateSessionCoreAsync(CancellationToken cancellationToken)
        => throw Refused();

    /// <inheritdoc/>
    protected override ValueTask<JsonElement> SerializeSessionCoreAsync(AgentSession session,
        JsonSerializerOptions? jsonSerializerOptions, CancellationToken cancellationToken)
        => throw Refused();

    /// <inheritdoc/>
    protected override ValueTask<AgentSession> DeserializeSessionCoreAsync(JsonElement serializedSession,
        JsonSerializerOptions? jsonSerializerOptions, CancellationToken cancellationToken)
        => throw Refused();

    /// <inheritdoc/>
    protected override Task<AgentResponse> RunCoreAsync(IEnumerable<ChatMessage> messages, AgentSession? session,
        AgentRunOptions? options, CancellationToken cancellationToken)
        => throw Refused();

    /// <inheritdoc/>
    protected override IAsyncEnumerable<AgentResponseUpdate> RunCoreStreamingAsync(IEnumerable<ChatMessage> messages,
        AgentSession? session, AgentRunOptions? options, CancellationToken cancellationToken)
        => throw Refused();
}
