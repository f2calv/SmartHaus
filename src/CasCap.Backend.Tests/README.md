# CasCap.Backend.Tests

Credential-free tests for SmartHaus backend and domain behavior, including MCP prompt contracts, KNX
summaries, camera clip policy, and Agent Runtime request translation.

## Purpose

Self-contained domain tests run without the ASP.NET Core entry host, credentials, configuration files,
or external inference providers. Generic text, binary, streaming, session, definition, and built-in
tool behavior is tested centrally through the Agent Runtime contracts in agentizr.

Host endpoint and application-bootstrap coverage lives in
[CasCap.App.Server.Tests](../CasCap.App.Server.Tests/README.md).

## Test Classes

| Class | Methods | Cases | Category | Description |
| --- | --- | --- | --- | --- |
| `McpPromptToolReferenceTests` | 1 | 1 | McpPrompts | Ensures shipped MCP prompts name only available tools |
| `KnxContactSummaryTests` | 4 | 13 | Knx | Verifies door and window contact summaries |
| `KnxDoorLockSummaryTests` | 4 | 7 | Knx | Verifies door lock summaries |
| `CameraClipQueueTests` | 7 | 7 | CameraClips | Verifies privacy filtering, mappings, cooldown, and bounded admission |
| `MediaBgServiceTests` | 1 | 3 | Media | Verifies stateless Agent Runtime requests for image, audio, and document events |
| `LiteSinkRegistrationTests` | 1 | 9 | Sinks | Asserts Redis sink selection and Azure Tables exclusion for nine lite registrations |
| `HausMcpToolSetTests` | 8 | 20 | Mcp | Feature-dependent MCP tool selection and the door-unlock approval gate |
| `ShutterTargetValidationTests` | 3 | 3 | Knx | Shutter target bounds |
| Total | 29 | 63 | | Source-declared cases, including theories |

## Trait Categories

| Category | Meaning |
| --- | --- |
| `McpPrompts` | MCP prompt contract checks |
| `Knx` | KNX contact and door-lock summaries |
| `CameraClips` | Camera event-to-clip admission and privacy policy |
| `Media` | Media-event translation into remote Agent Runtime requests |
| `Sinks` | Event-sink selection for lite registration |
| `Mcp` | Feature-dependent MCP tool registration |

## Skipped Tests

There are no skipped tests.

## Agent Evaluation

Agent definitions are evaluated in agentizr against scenarios supplied with the definition, before
activation. This project keeps no evaluation scenarios or fixtures.

## Running The Tests

Use a .NET 10 SDK with xUnit v3 and Microsoft.Testing.Platform from the repository root.
The inventory above was derived from source; it does not report execution results.

```bash
# Credential-free domain tests
dotnet test --project src/CasCap.Backend.Tests/CasCap.Backend.Tests.csproj
```

## Test Layout

```text
CasCap.Backend.Tests/
├── Tests/
│   ├── Unit/                # Self-contained backend and domain tests
│   └── McpPromptContract.cs # Prompt-to-tool reference check
├── GlobalUsings.cs
└── xunit.runner.json
```

## Dependencies

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Backend` | Backend orchestration, MCP tools, and domain services under test |
| `CasCap.Api.Knx` | KNX summaries and internal lookup behavior under test |

### Packages

| Package | Purpose |
| --- | --- |
| `Microsoft.Testing.Extensions.CodeCoverage` | Microsoft.Testing.Platform coverage integration |
| `xunit.v3` | Test framework and runner |

The project intentionally does not reference `Microsoft.AspNetCore.Mvc.Testing`. Test collections run
serially through `xunit.runner.json`.

## License

This project is released under the [MIT License](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
