# CasCap.App.Console

A Spectre.Console-based interactive client for exercising tenant-scoped agents hosted by the remote
Agent Runtime.

## Purpose

`CasCap.App.Console` is a developer tool for exercising the same immutable agent definitions used by
SmartHaus deployments. Agent construction, providers, credentials, tools, prompts and session state
remain owned by the Agent Runtime; the console sends prompts and renders execution events and the
final response.

`Program.cs` is a thin entry point. `AppHost.cs` owns logging, configuration, host construction,
cancellation, and execution, while `AppHost.Features.cs` owns the lite feature and MCP registrations.

### Startup Sequence

1. Configures Serilog logging (Warning minimum, Information for `CasCap` namespace).
2. Calls `InitializeConfiguration` from `CasCap.App` to load the standard configuration providers.
3. Registers the typed Agent Runtime client and optional certificate bearer authentication.
4. Launches the interactive `ConsoleApp` session.

### Interactive Loop

- **Agent selector**: presents the configured `AgentRuntimeConsoleConfig.AgentNames`; auto-selects when only one is configured.
- **Prompt input**: custom line editor with live approximate token count (`cl100k_base` tokenizer), Up/Down history navigation, and Ctrl+Left/Right word boundary movement.
- **Streaming events**: delegation and compaction activity updates the waiting status while the runtime executes.
- **Session summary**: reports the definition version, model, timing, usage, tool calls, attachments and session status returned by the runtime.
- **Navigation**: Escape returns to agent selector; `exit`/`quit` or Ctrl+C ends the session.

## Configuration

Uses the same configuration provider chain as the server application. The endpoint comes from
`AgentRuntimeClientOptions`; optional Entra certificate authentication comes from
`AgentRuntimeAzureAuthConfig`.

`AgentRuntimeConsoleConfig` has public-safe defaults and supports these overrides:

| Property | Default | Purpose |
| --- | --- | --- |
| `AgentNames` | `CommsAgent` | Tenant agent names shown in the selector |
| `SessionId` | `smarthaus-console` | Caller-owned persistent session identifier |
| `DiagnosticDetailsEnabled` | `false` | Requests operator-only diagnostic properties |

## Configuration Examples

The built-in defaults select the SmartHaus communications agent with a persistent console session:

```json
{
  "CasCap": {
    "AgentRuntimeConsoleConfig": {
      "AgentNames": ["CommsAgent"]
    }
  }
}
```

A local override can expose several tenant agents and request operator diagnostics:

```json
{
  "CasCap": {
    "AgentRuntimeConsoleConfig": {
      "AgentNames": ["CommsAgent", "SecurityAgent"],
      "SessionId": "smarthaus-console",
      "DiagnosticDetailsEnabled": true
    }
  }
}
```

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

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.App` | Shared configuration bootstrap (`InitializeConfiguration`) |
| `CasCap.Common.Hosting.AspNetCore` | Serilog structured logging pipeline |

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
