# CasCap.Api.Buderus.Tests

Integration tests for the Buderus heating system library ([CasCap.Api.Buderus](../CasCap.Api.Buderus)) exercising `BuderusKm200ClientService` against a real Buderus KM200 controller.

## Purpose

These tests verify that the KM200 encrypted HTTPS API can be reached, datapoints can be discovered and decoded, and that the response objects map correctly to the `Km200DatapointObject` DTOs.

## Tests

Counts are source-declared, including skipped cases; tests have not been executed for this documentation.

| Class | Methods | Expanded cases | Categories |
| --- | ---: | ---: | --- |
| `BuderusKm200ClientServiceTests` | 12 | 14 | `Integration` |

The invalid-path theory has three inline cases. All methods carry the class-level `Integration`
trait, including validation-only cases.

## Skipped Tests

| Reason | Methods / cases |
| --- | --- |
| Requires a reachable Buderus KM200 device — changes live settings | 2 / 2 |

The remaining integration tests are not automatically skipped when credentials are absent.
`SetDataPoint_DhwOperationMode_Roundtrip` is active and changes a live setting.

## Test Layout

```text
CasCap.Api.Buderus.Tests/
├── BuderusKm200ClientServiceTests.cs
└── TestBase.cs
```

This project currently keeps its tests at the project root rather than under `Tests/`.

## Prerequisites

- A running Buderus KM200 controller accessible from the test host.
- `appsettings.json` with `AppConfig`, `ConnectionStrings`, and `CasCap:BuderusConfig` sections.
- A matching gateway password and private password, supplied through local configuration,
  user secrets or secret-backed environment variables.
- `TestBase` loads standard configuration and optional Key Vault through `AzureAuthConfig`.
  Configure only services you intend to access; do not put credentials in tracked appsettings.

## Running the tests

```bash
dotnet test --project src/CasCap.Api.Buderus.Tests/CasCap.Api.Buderus.Tests.csproj
```

Run from the repository root using the .NET 10 SDK, xUnit v3 and Microsoft.Testing.Platform.
Run only with explicit approval to access and modify the configured device.

## Dependencies

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.Api.Buderus` | Library under test |
| `CasCap.Api.Azure.Auth` | Azure authentication for integration tests |
| `CasCap.Common.Configuration` | `AddStandardConfiguration` / `AddKeyVaultConfigurationFrom` |
| `CasCap.Common.Extensions` | Shared extension helpers |
| `CasCap.Common.Logging` | xUnit logging integration |
| `CasCap.Common.Net` | HTTP helpers |
| `CasCap.Common.Testing` | `AddXUnitLogging` and test utilities |

## License

This project is released under the [MIT License](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
