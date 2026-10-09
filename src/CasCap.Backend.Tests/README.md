# CasCap.Backend.Tests

Tests for SmartHaus backend and domain behavior, including MCP contracts, KNX summaries, camera clip
policy, live inference clients, and the SmartHaus agent evaluation suite.

## Purpose

Self-contained domain tests run without the ASP.NET Core entry host. Integration tests use the root
configuration chain and optional local test settings to exercise configured inference providers and
the shared `CasCap.Common.AI.Evaluation` harness.

Host endpoint and application-bootstrap coverage lives in
[CasCap.App.Server.Tests](../CasCap.App.Server.Tests/README.md).

## Test Classes

| Class | Methods | Cases | Category | Description |
| --- | --- | --- | --- | --- |
| `AgentEvaluationTests` | 1 | 4 | Integration, AgentEvaluation | Runs each agent evaluation scenario against configured model and variant combinations |
| `AgentToolSurfaceTests` | 1 | 1 | Integration, ToolSurface | Measures agent tool surfaces without calling a model |
| `AIAgentExtensionsTests` | 3 | 3 | Integration | Runs text, image, and resumed-session analysis against a live inference server |
| `LlamaCppApiClientTests` | 3 | 3 | Integration | Exercises response, streaming, and file content through the llama.cpp-compatible client |
| `AgentEvaluationScenarioTests` | 5 | 11 | AgentEvaluation | Verifies graders, tool classification, fixtures, and scenario integrity |
| `McpPromptToolReferenceTests` | 1 | 1 | McpPrompts | Ensures shipped MCP prompts name only available tools |
| `KnxContactSummaryTests` | 4 | 13 | Knx | Verifies door and window contact summaries |
| `KnxDoorLockSummaryTests` | 4 | 7 | Knx | Verifies door lock summaries |
| `CameraClipQueueTests` | 7 | 7 | CameraClips | Verifies privacy filtering, mappings, cooldown, and bounded admission |
| `MediaBgServiceTests` | 1 | 3 | Media | Verifies stateless Agent Runtime requests for image, audio, and document events |

## Trait Categories

| Category | Meaning |
| --- | --- |
| `Integration` | Reads configuration or credentials, or reaches an external inference service |
| `AgentEvaluation` | Agent evaluation scenarios and model runs |
| `ToolSurface` | Agent tool surface measurements |
| `McpPrompts` | MCP prompt contract checks |
| `Knx` | KNX contact and door-lock summaries |
| `CameraClips` | Camera event-to-clip admission and privacy policy |
| `Media` | Media-event translation into remote Agent Runtime requests |

## Skipped Tests

| Test | Reason | Count |
| --- | --- | --- |
| `LlamaCppApiClientTests` | Depends on an uncommitted image fixture | 1 |

`AgentEvaluationTests` skips a scenario at run time when no configured provider has an endpoint and
credential on the current machine.

## Agent Evaluation

The suite asks the production SmartHaus agents household questions against configured edge and Azure
models, instruction variants, and tool-surface variants. It records pass rates, latency, token usage,
throttling, and reasoning output through the reusable `CasCap.Common.AI.Evaluation` harness.

`SmartHausEvaluationTestData` binds the harness to the `CasCap.Backend` assembly for MCP tool types
and embedded instructions. `SyntheticHouseTestData` supplies deterministic tool responses; commands
that could mutate the house are recorded and rejected rather than executed.

Each evaluation session writes JSONL run records and Markdown summaries beneath the configured
`ResultsDirectory`. The default is `TestResults/AgentEvaluation`.

### Configuration

Settings bind from `CasCap:AgentEvaluationConfig`. Provider endpoints and private settings belong in
the gitignored `appsettings.Tests.Local.json` beside this README.

| Setting | Default | Meaning |
| --- | --- | --- |
| `ProviderKeys` | `EdgeGpu` | Provider keys to evaluate |
| `Repetitions` | `3` | Runs per model and variant combination |
| `InstructionVariants` | all | Instruction variants included beside the baseline |
| `ToolSurfaceVariants` | all | Tool-surface variants included beside the baseline |
| `MinimumPassRate` | `0.5` | Minimum asserted baseline pass rate |
| `ReferenceProviderKey` | first provider | Reference model for relative-speed reporting |
| `RunTimeoutSeconds` | `300` | Limit for one run, including delegation |
| `WarmUpTimeoutSeconds` | `1800` | Limit for the unmeasured model warm-up request |
| `ResultsDirectory` | `TestResults/AgentEvaluation` | Evaluation output directory |
| `ProviderReasoningEffortApplied` | `false` | Applies each provider's configured reasoning effort |
| `EndpointToolsMapped` | `false` | Offers endpoint tool sources |

## Running The Tests

```bash
# Self-contained domain tests
dotnet test --project src/CasCap.Backend.Tests/CasCap.Backend.Tests.csproj --filter-not-trait "Category=Integration"

# Tool surface measurements without model calls
dotnet test --project src/CasCap.Backend.Tests/CasCap.Backend.Tests.csproj --filter-class "*AgentToolSurfaceTests"

# Agent evaluation against configured models
dotnet test --project src/CasCap.Backend.Tests/CasCap.Backend.Tests.csproj --filter-class "*AgentEvaluationTests"
```

## Test Layout

```text
CasCap.Backend.Tests/
├── Misc/                    # Live inference server tests
├── Tests/
│   ├── Integration/         # Agent evaluation and tool surface tests
│   ├── Unit/                # Self-contained backend and domain tests
│   ├── AgentEvaluationScenarioTestData.cs
│   ├── SmartHausEvaluationTestData.cs
│   └── SyntheticHouseTestData.cs
├── GlobalUsings.cs
├── TestBase.cs              # Shared integration-test configuration and logging
└── xunit.runner.json
```

## Dependencies

### Project References

| Project | Purpose |
| --- | --- |
| `CasCap.Backend` | Backend orchestration, MCP tools, agent extensions, and domain services under test |
| `CasCap.Api.Knx` | KNX summaries and internal lookup behavior under test |
| `CasCap.Common.AI.Evaluation` | Agent evaluation harness, graders, and reports |
| `CasCap.Common.Configuration` | Standard configuration and Key Vault configuration helpers |
| `CasCap.Common.Testing` | xUnit logging and shared test utilities |

### Packages

| Package | Purpose |
| --- | --- |
| `Microsoft.Testing.Extensions.CodeCoverage` | Microsoft.Testing.Platform coverage integration |
| `xunit.v3` | Test framework and runner |

The project intentionally does not reference `Microsoft.AspNetCore.Mvc.Testing`. Test collections run
serially through `xunit.runner.json`.

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
