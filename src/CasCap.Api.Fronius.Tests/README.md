# CasCap.Api.Fronius.Tests

Live Solar API integration tests and offline DTO regression tests for
[CasCap.Api.Fronius](../CasCap.Api.Fronius/README.md).

## Purpose

These tests verify that each Solar API v1 endpoint returns well-formed data and that the response DTOs map correctly.

## Tests

Counts are source-declared, including skipped cases; they are not observed execution results.

| Class | Methods | Expanded cases | Categories |
| --- | ---: | ---: | --- |
| `FroniusClientServiceTests` | 7 | 14 | `Integration` |
| `UnitValueTests` | 1 | 2 | `Deserialization` |
| Total | 8 | 16 | |

`ApiPayloadMatchesDtoContract` has eight live endpoint cases. The offline theory accepts both
scalar `Value` and per-phase `Values` firmware payloads.

## Skipped Tests

| Test | Reason | Cases |
| --- | --- | ---: |
| `GetMeterRealtimeData_ReturnsData` | planned in 1.13 | 1 |

Other live cases do not automatically skip when hardware is unavailable.

## Test Layout

```text
CasCap.Api.Fronius.Tests/
├── FroniusClientServiceTests.cs
├── JsonContractAuditor.cs
├── TestBase.cs
└── Tests/
    └── Unit/
        └── UnitValueTests.cs
```

## Prerequisites

- A running Fronius inverter accessible from the test host.
- `appsettings.json` with `AppConfig`, `ConnectionStrings`, and `CasCap:FroniusConfig` sections.
- A configured inverter endpoint in local configuration, not tracked environment files.
- `TestBase` loads standard configuration and optional Key Vault through `AzureAuthConfig`.

## Running the tests

```bash
# Offline regression cases only
dotnet test --project src/CasCap.Api.Fronius.Tests/CasCap.Api.Fronius.Tests.csproj -- --filter-trait "Category=Deserialization"
```

Use .NET 10, xUnit v3 and Microsoft.Testing.Platform from the repository root. Omit the trait filter
only when explicitly approved to access the live inverter.

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

This project is released under the [MIT License](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
