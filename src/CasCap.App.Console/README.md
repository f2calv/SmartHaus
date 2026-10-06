# CasCap.App.Console

A Spectre.Console-based interactive terminal application for local MCP/AI agent development and testing. Connects to configured AI providers (Ollama, OpenAI, Azure OpenAI, Azure AI Foundry) and exposes in-process MCP tools from the feature libraries.

## Purpose

`CasCap.App.Console` is a developer tool for exercising AI agents against the home automation MCP tools without deploying to Kubernetes. It registers a subset of feature libraries in "lite" mode (monitor background services disabled) and runs an interactive prompt loop with streaming responses.

`Program.cs` is a thin entry point. `AppHost.cs` owns logging, configuration, host construction,
cancellation, and execution, while `AppHost.Features.cs` owns the lite feature and MCP registrations.

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
| `CasCap.Common.Hosting.AspNetCore` | Serilog structured logging pipeline |
| `CasCap.Common.AI` | Consolidated MCP tool and prompt registration for all smart-home integrations |
| `CasCap.Api.Fronius.Sinks` | Fronius event sinks |
| `CasCap.Api.Buderus.Sinks` | Buderus event sinks |
| `CasCap.Api.Knx.Sinks` | KNX event sinks |
| `CasCap.Api.DoorBird.Sinks` | DoorBird event sinks |

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
