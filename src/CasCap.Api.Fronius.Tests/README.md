# CasCap.Api.Fronius.Tests

Integration tests for the Fronius solar inverter library ([CasCap.Api.Fronius](../CasCap.Api.Fronius)) exercising `FroniusClientService` against a real Fronius Symo Gen24 inverter.

## Purpose

These tests verify that each Solar API v1 endpoint returns well-formed data and that the response DTOs map correctly.

### Test classes

| Class | Description |
| --- | --- |
| `FroniusClientServiceTests` | Integration tests for `GetPowerFlowRealtimeData`, `GetInverterRealtimeData`, `GetInverterInfo`, `GetMeterRealtimeData`, `GetStorageRealtimeData`, and other Solar API endpoints |

## Prerequisites

- A running Fronius inverter accessible from the test host.
- `appsettings.json` with `AppConfig`, `ConnectionStrings`, and `CasCap:FroniusConfig` sections.
- `appsettings.Development.json` (optional) with the inverter's local IP address.

## Running the tests

```bash
dotnet test src/CasCap.Api.Fronius.Tests/CasCap.Api.Fronius.Tests.csproj
```

## Dependencies

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.Api.Fronius` | Library under test |
| `CasCap.Api.Azure.Auth` | Azure authentication for integration tests |
| `CasCap.Common.Configuration` | `AddStandardConfiguration` / `AddKeyVaultConfigurationFrom` |
| `CasCap.Common.Extensions` | Shared extension helpers |
| `CasCap.Common.Logging` | xUnit logging integration |
| `CasCap.Common.Net` | HTTP helpers |
| `CasCap.Common.Testing` | `AddXUnitLogging` and test utilities |

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
