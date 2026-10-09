# CasCap.Backend.Tests

Credential-free tests for SmartHaus backend and domain behavior, including MCP contracts, evaluation
scenarios, KNX summaries, camera clip policy, and Agent Runtime request translation.

## Purpose

Self-contained domain tests run without the ASP.NET Core entry host, credentials, configuration files,
or external inference providers. Generic text, binary, streaming, session, definition, and built-in
tool behavior is tested centrally through the Agent Runtime contracts in agentizr.

Host endpoint and application-bootstrap coverage lives in
[CasCap.App.Server.Tests](../CasCap.App.Server.Tests/README.md).

## Test Classes

| Class | Methods | Cases | Category | Description |
| --- | --- | --- | --- | --- |
| `AgentEvaluationScenarioTests` | 5 | 11 | AgentEvaluation | Verifies graders, tool classification, fixtures, and scenario integrity |
| `McpPromptToolReferenceTests` | 1 | 1 | McpPrompts | Ensures shipped MCP prompts name only available tools |
| `KnxContactSummaryTests` | 4 | 13 | Knx | Verifies door and window contact summaries |
| `KnxDoorLockSummaryTests` | 4 | 7 | Knx | Verifies door lock summaries |
| `CameraClipQueueTests` | 7 | 7 | CameraClips | Verifies privacy filtering, mappings, cooldown, and bounded admission |
| `MediaBgServiceTests` | 1 | 3 | Media | Verifies stateless Agent Runtime requests for image, audio, and document events |

## Trait Categories

| Category | Meaning |
| --- | --- |
| `AgentEvaluation` | Agent evaluation scenarios and model runs |
| `McpPrompts` | MCP prompt contract checks |
| `Knx` | KNX contact and door-lock summaries |
| `CameraClips` | Camera event-to-clip admission and privacy policy |
| `Media` | Media-event translation into remote Agent Runtime requests |

## Skipped Tests

There are no skipped tests.

## Agent Evaluation

`AgentEvaluationScenarioTestData` and `SyntheticHouseTestData` remain SmartHaus-owned domain assets.
Their unit tests verify answer grading, tool classification, deterministic fixture behavior, and that
every scenario names a known SmartHaus MCP tool, runtime built-in, or agent delegation. They do not
construct local agents or duplicate Agent Runtime execution tests.

## Running The Tests

```bash
# Credential-free domain tests
dotnet test --project src/CasCap.Backend.Tests/CasCap.Backend.Tests.csproj
```

## Test Layout

```text
CasCap.Backend.Tests/
├── Tests/
│   ├── Unit/                # Self-contained backend and domain tests
│   ├── AgentEvaluationScenarioTestData.cs
│   ├── SmartHausEvaluationTestData.cs
│   └── SyntheticHouseTestData.cs
├── GlobalUsings.cs
└── xunit.runner.json
```

## Dependencies

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Backend` | Backend orchestration, MCP tools, and domain services under test |
| `CasCap.Api.Knx` | KNX summaries and internal lookup behavior under test |
| `CasCap.Common.AI.Evaluation` | Scenario contracts, graders, fixture tools, and MCP catalog inspection |

### Packages

| Package | Purpose |
| --- | --- |
| `Microsoft.Testing.Extensions.CodeCoverage` | Microsoft.Testing.Platform coverage integration |
| `xunit.v3` | Test framework and runner |

The project intentionally does not reference `Microsoft.AspNetCore.Mvc.Testing`. Test collections run
serially through `xunit.runner.json`.

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
