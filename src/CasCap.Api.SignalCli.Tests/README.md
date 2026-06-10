# CasCap.Api.SignalCli.Tests

Integration and unit tests for the Signal messenger library ([CasCap.Api.SignalCli](../CasCap.Api.SignalCli)) exercising `SignalCliRestClientService` against a running [signal-cli REST API](https://bbernhard.github.io/signal-cli-rest-api/) instance, plus self-contained unit tests for WebSocket URI construction, JSON deserialization, and DI registration.

## Tests

| Class | Folder | Methods | Test Cases |
| --- | --- | --- | --- |
| `SignalCliJsonRpcClientServiceUnitTests` | Unit | 7 | 9 |
| `SignalCliRestClientServiceTests` | Integration | 52 | 52 |
| `SignalCliJsonRpcClientServiceTests` | Integration | 4 | 4 |
| **Total** | | **63** | **65** |

## Trait Categories

| Category | Description |
| --- | --- |
| `Integration` | Tests requiring a running signal-cli REST API instance |
| `WebSocket` | Self-contained unit tests for WebSocket/JSON-RPC logic |

## Skipped Tests

| Skip Reason | Count |
| --- | --- |
| Requires signal-cli REST API running in json-rpc mode | 3 |
| Requires a dedicated test phone number | 2 |
| Destructive or cannot be undone | 4 |
| Requires a second phone number | 2 |
| Requires a real challenge token / device-link URI / invite link / pack ID | 4 |
| One-shot setup already completed | 1 |
| Requires an untrusted identity to trust | 1 |
| VerifyNumber requires dedicated test phone number and token | 1 |
| **Total** | **18** |

## File Structure

```text
CasCap.Api.SignalCli.Tests/
├── CasCap.Api.SignalCli.Tests.csproj
├── GlobalUsings.cs
├── README.md
├── xunit.runner.json
└── Tests/
    ├── Unit/
    │   └── SignalCliJsonRpcClientServiceUnitTests.cs
    └── Integration/
        ├── TestBase.cs
        ├── SignalCliRestClientServiceTests.cs
        └── SignalCliJsonRpcClientServiceTests.cs
```

## Prerequisites

- A running signal-cli REST API accessible from the test host.
- `appsettings.json` / `appsettings.Local.json` with `CasCap:SignalCliConfig` section including `BaseAddress`, `PhoneNumber`, and `BasicAuthEnabled`.
- `CasCap:AIConfig:CommsAgent:Settings:GroupName` for group-related integration tests.

## Running the Tests

```bash
dotnet test src/CasCap.Api.SignalCli.Tests/CasCap.Api.SignalCli.Tests.csproj
```

## Dependencies

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.Api.SignalCli` | Library under test |
| `CasCap.Api.Azure.Auth` | Azure authentication for Key Vault configuration |
| `CasCap.Common.Configuration` | `AddStandardConfiguration` / `AddKeyVaultConfigurationFrom` |
| `CasCap.Common.Extensions` | Shared extension helpers |
| `CasCap.Common.Logging` | xUnit logging integration |
| `CasCap.Common.Net` | HTTP helpers |
| `CasCap.Common.Testing` | `AddXUnitLogging` and test utilities |

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
