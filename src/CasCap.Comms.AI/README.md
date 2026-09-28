# CasCap.Comms.AI

The AI agent responder for the [CasCap.Comms](../CasCap.Comms/README.md) Signalizr communications
pipeline.

## Purpose

`AgentCommsResponder` implements `ICommsResponder` with a configured `AIAgent`. It:

- turns chat-bound stream events and inbound messages into agent turns, with conversation sessions,
  model and instruction overrides and optional session bypass;
- handles slash commands through `AgentCommandHandler` and poll votes through `IPollTracker`;
- reports sub-agent delegation with a reaction swap and, optionally, a status message;
- appends a stats footer to each reply and posts a pipeline timeline, stream-event copies and
  compaction notices to the monitor group through `CommsDebugNotifier`.

When the agent profile, its provider or the keyed agent is missing, the responder reports itself
unavailable and the pipeline behaves as if no responder were registered.

This project is maintained **byte-identically** in the SmartHaus and CAS repositories until the
shared communications code moves to its own repository. Change both copies in the same change.

## Public Surface

| Type | Purpose |
| --- | --- |
| `AgentCommsResponder` | `ICommsResponder` backed by an `AIAgent` |
| `CommsDebugNotifier` | Stats footer and monitor-group diagnostics |
| `IAgentRunEnricher` | Host seam for measurements around each run, such as GPU energy use |
| `CommsAgentProfile` | The agent key and the assembly holding its embedded instructions |
| `CommsDebugStep` | One step of the monitor-group pipeline timeline |
| `MessagingMcpQueryService` | MCP poll tools (`create_poll`, `close_poll`, `get_poll_status`) on the chat group |
| `CommsAgentServiceCollectionExtensions` | `AddCommsAgent(agentKey, instructionsAssembly)`, `AddMessagingMcp()` and `AddMessagingMcpStub()` |

## Configuration

The responder reads `CommsConfig` from [CasCap.Comms](../CasCap.Comms/README.md) and the agent
profile named by `CommsAgentProfile.AgentKey` from `CasCap:AIConfig:Agents`. The host registers the
keyed `AIAgent` for that key.

```csharp
services.AddComms(configuration);
services.AddCommsAgent("CommsAgent", typeof(Program).Assembly);
services.AddSingleton<IAgentRunEnricher, MyHardwareEnricher>();
```

## Dependencies

| Dependency | Use |
| --- | --- |
| `CasCap.Comms` | The pipeline this responder answers for |
| `CasCap.Common.AI` | Agent runs, sessions, slash commands, poll tracking and MCP attributes |

Debug builds reference adjacent source checkouts; Release builds use the published packages.
