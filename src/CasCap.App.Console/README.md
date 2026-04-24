# CasCap.App.Console

A Spectre.Console-based interactive terminal application for local MCP/AI agent development and testing. Connects to configured AI providers (Ollama, OpenAI, Azure OpenAI, Azure AI Foundry) and exposes in-process MCP tools from the feature libraries.

## Purpose

`CasCap.App.Console` is a developer tool for exercising AI agents against the home automation MCP tools without deploying to Kubernetes. It registers a subset of feature libraries in "lite" mode (monitor background services disabled) and runs an interactive prompt loop with streaming responses.

### Startup Sequence

1. Configures Serilog logging (Warning minimum, Information for `CasCap` namespace).
2. Calls `InitializeConfiguration` from `CasCap.App` to bootstrap all strongly-typed options.
3. Registers feature libraries with their sinks and MCP tool services (lite mode — no polling).
4. Launches a `ConsoleApp` interactive session.

### Registered Features

| Feature | Registration | MCP tools |
| --- | --- | --- |
| Fronius | `AddFroniusWithExtraSinks` (lite) + `AddInverterMcp` | `InverterMcpQueryService` |
| Buderus | `AddBuderusWithExtraSinks` (lite) + `AddHeatPumpMcp` | `HeatPumpMcpQueryService` |
| DoorBird | `AddDoorBirdWithExtraSinks` (lite) + `AddFrontDoorMcp` | `FrontDoorMcpQueryService` |
| KNX | `AddKnxWithExtraSinks` (lite) + `AddBusSystemMcp` | `BusSystemMcpQueryService` |

### Interactive Loop

- **Agent selector**: presents all agents from `AIConfig.Agents`; auto-selects when only one is configured.
- **Tool discovery**: gathers in-process MCP tools (from `ToolSource.Service`) and remote MCP tools (from `ToolSource.Endpoint`), with include/exclude filtering per `ToolSource`.
- **Prompt discovery**: gathers in-process MCP prompts (from `PromptSource.Service`) and remote MCP prompts (from `PromptSource.Endpoint`), with include/exclude filtering per `PromptSource`.
- **Prompt input**: custom line editor with live approximate token count (`cl100k_base` tokenizer), Up/Down history navigation, and Ctrl+Left/Right word boundary movement.
- **Streaming output**: thinking/reasoning content rendered in grey, regular text in default colour.
- **Session summary**: two-column panel showing provider, agent, usage statistics, and middleware diagnostics.
- **Navigation**: Escape returns to agent selector; `exit`/`quit` or Ctrl+C ends the session.

### Slash Commands

Typing a slash-command at the prompt intercepts the input before it reaches the AI agent. The same commands are also recognised by `CommunicationsBgService` when received via the Signal messenger interface. Available commands (defined in the `ChatCommand` enum in `CasCap.Common.AI`):

| Command | Description |
| --- | --- |
| `/help` | List all available commands. |
| `/session info` | Display technical information about the current session (size in bytes, message count, StateBag keys). |
| `/session reset` | Discard the current session and start a fresh conversation on the next message. |
| `/session bypass <prompt>` | Send a one-off prompt to the agent without loading or saving the active session. |
| `/session compact <count>` | Reduce the session to the newest N messages, removing older history. |
| `/session disable` | Disable session persistence; each message starts a fresh conversation. |
| `/session enable` | Re-enable session persistence. |
| `/session save <name>` | Save a named snapshot of the active session for later analysis. |
| `/session load <name>` | Load a previously saved snapshot into the active session. |
| `/session delete <name>` | Delete a previously saved session snapshot. |
| `/model <modelName>` | Override the model used for subsequent requests. Omit the argument to print the current override. |

## Configuration

Uses the same `appsettings.json` / `appsettings.Development.json` as the server application. AI agent selection is driven by the `AIConfig` section.

### Required Data Files

| File | Description |
| --- | --- |
| `appsettings.json` | Application configuration |
| `appsettings.Development.json` | Development overrides |

## Dependencies

### NuGet packages

| Package | Purpose |
| --- | --- |
| [Microsoft.Extensions.Hosting](https://www.nuget.org/packages/microsoft.extensions.hosting) | Generic host builder |
| [Microsoft.ML.Tokenizers.Data.Cl100kBase](https://www.nuget.org/packages/microsoft.ml.tokenizers.data.cl100kbase) | Approximate token counting for prompt input |
| [Spectre.Console](https://www.nuget.org/packages/spectre.console) | Rich terminal UI (markup, tables, status spinners) |
| [Spectre.Console.ImageSharp](https://www.nuget.org/packages/spectre.console.imagesharp) | Image rendering in terminal |
| [Spectre.Console.Json](https://www.nuget.org/packages/spectre.console.json) | JSON rendering in terminal |
| [CasCap.Common.Net](https://www.nuget.org/packages/cascap.common.net) | HTTP client helpers |

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.App` | Shared configuration bootstrap (`InitializeConfiguration`) |
| `CasCap.Common.Logging.Serilog` | Serilog structured logging pipeline |
| `CasCap.Common.AI` | Consolidated MCP tool and prompt registration for all smart-home integrations |
| `CasCap.Api.Fronius.Sinks` | Fronius event sinks |
| `CasCap.Api.Buderus.Sinks` | Buderus event sinks |
| `CasCap.Api.Knx.Sinks` | KNX event sinks |
| `CasCap.Api.DoorBird.Sinks` | DoorBird event sinks |

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
