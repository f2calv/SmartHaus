# CasCap.Api.Buderus.Tests

Integration tests for the Buderus heating system library ([CasCap.Api.Buderus](../CasCap.Api.Buderus)) exercising `BuderusKm200ClientService` against a real Buderus KM200 controller.

## Purpose

These tests verify that the KM200 encrypted HTTPS API can be reached, datapoints can be discovered and decoded, and that the response objects map correctly to the `Km200DatapointObject` DTOs.

### Test classes

| Class | Description |
| --- | --- |
| `BuderusKm200ClientServiceTests` | Integration tests for KM200 datapoint discovery, `GetDataPoint` retrieval, and datapoint filtering logic |

## Prerequisites

- A running Buderus KM200 controller accessible from the test host.
- `appsettings.json` with `AppConfig`, `ConnectionStrings`, and `CasCap:BuderusConfig` sections.
- `appsettings.Development.json` (optional) with the KM200's local IP address and gateway password.

## Running the tests

```bash
dotnet test src/CasCap.Api.Buderus.Tests/CasCap.Api.Buderus.Tests.csproj
```

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

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
