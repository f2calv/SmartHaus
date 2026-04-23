# CasCap.App.Tests

Integration tests for the [CasCap.App](../CasCap.App) application, covering cross-cutting concerns such as AI agent integration, KNX group address lookups, and miscellaneous utilities.

## Purpose

These tests exercise components that span multiple feature libraries and require the full `AppConfig` and dependency injection container to be initialised. They run against live external services (Ollama, Azure Table Storage, KNX configuration).

### Test classes

| Class | Description |
| --- | --- |
| `AIAgentExtensionsTests` | Integration tests for `AgentExtensions.RunAnalysisAsync` against a live Ollama or OpenAI-compatible inference server. Tests cover multi-turn conversation, tool invocation (via KNX and other query services), and structured response parsing |
| `LlamaCppApiClientTests` | Integration tests for the llama.cpp REST API client |
| `FeatureServiceRegistrationTests` | Integration tests verifying `IBgFeature` background-service registrations and the `EnabledFeatures` filtering behaviour |
| `HealthTests` | Integration tests for the application health-check endpoints (`/healthz`) |
| `SystemControllerTests` | Integration tests for the `SystemController` (`GET /api/system`) including Basic authentication |

## Prerequisites

- `appsettings.json` with `AppConfig`, `AIConfig`, `ConnectionStrings`, and `KnxConfig` sections.
- `appsettings.Development.json` (optional) with server URLs and secrets.
- `knxgroupaddresses.xml` accessible via the `GroupAddressXmlFilePath` configured in `appsettings.Development.json`.
- A running inference server e.g. Ollama for `AIAgentExtensionsTests` — tests fail gracefully when the server is unreachable.

## Running the tests

```bash
dotnet test src/CasCap.App.Tests/CasCap.App.Tests.csproj
```

## Dependencies

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.App.Server` | ASP.NET Core host under test |
| `CasCap.Api.Knx` | KNX service and group address lookup under test |
| `CasCap.SmartHaus` | AI agent extensions and hub services under test |
| `CasCap.Common.Configuration` | `AddStandardConfiguration` / `AddKeyVaultConfigurationFrom` |
| `CasCap.Common.Testing` | `AddXUnitLogging` and test utilities |

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
