# CasCap.Api.SignalCli.Tests

Integration tests for the Signal messenger library ([CasCap.Api.SignalCli](../CasCap.Api.SignalCli)) exercising `SignalCliRestClientService` against a running [signal-cli REST API](https://bbernhard.github.io/signal-cli-rest-api/) instance.

## Purpose

These tests verify that the full signal-cli API surface works correctly — from version retrieval to message sending, device management, group operations, and profile updates.

### Test classes

| Class | Description |
| --- | --- |
| `SignalCliRestClientServiceTests` | Integration tests covering `GetAbout`, `SendMessage`, `ListAccounts`, `GetQrCodeLink`, `ListLinkedDevices`, `ListGroups`, `ListAttachments`, `UpdateProfile`, and typing indicator endpoints |
| `SignalCliJsonRpcClientServiceTests` | Unit tests for WebSocket URI construction and transport-mode defaults, plus integration tests against a signal-cli REST API instance running in `json-rpc` mode |

## Prerequisites

- A running signal-cli REST API accessible from the test host.
- `appsettings.json` with `AppConfig`, `ConnectionStrings`, `CasCap:SignalCliConfig` and `CasCap:CommsAgentConfig` sections including `BaseAddress`, `PhoneNumber` and `GroupName`.
- `appsettings.Development.json` (optional) with the signal-cli server URL.

> Tests that require a registered phone number (e.g. `SendMessage_ToSelf`) are automatically skipped when `SignalCliConfig.PhoneNumber` is not configured.

## Running the tests

```bash
dotnet test src/CasCap.Api.SignalCli.Tests/CasCap.Api.SignalCli.Tests.csproj
```

## Dependencies

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.Api.SignalCli` | Library under test |
| `CasCap.Api.Azure.Auth` | Azure authentication for integration tests |
| `CasCap.Common.Configuration` | `AddStandardConfiguration` / `AddKeyVaultConfigurationFrom` |
| `CasCap.Common.Extensions` | Shared extension helpers |
| `CasCap.Common.Logging` | xUnit logging integration |
| `CasCap.Common.Net` | HTTP helpers |
| `CasCap.Common.Testing` | `AddXUnitLogging` and test utilities |


## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
