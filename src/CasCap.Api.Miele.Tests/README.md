# CasCap.Api.Miele.Tests

Integration tests for the Miele appliance library ([CasCap.Api.Miele](../CasCap.Api.Miele)) exercising `MieleClientService` against the live Miele 3rd Party Cloud API.

## Purpose

These tests verify that the OAuth 2.0 token flow works correctly and that the Miele Cloud API endpoints return well-formed appliance data.

### Test classes

| Class | Description |
| --- | --- |
| `MieleClientServiceTests` | Integration tests for `GetDevices`, `GetDevice`, `GetState`, `GetActions`, and `GetPrograms` endpoints |

## Prerequisites

- A Miele account with at least one registered appliance.
- `appsettings.json` with `AppConfig`, `ConnectionStrings`, and `CasCap:MieleConfig` sections including a valid OAuth client ID and secret.
- `appsettings.Development.json` (optional) with OAuth credentials.

## Running the tests

```bash
dotnet test src/CasCap.Api.Miele.Tests/CasCap.Api.Miele.Tests.csproj
```

## Dependencies

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.Api.Miele` | Library under test |
| `CasCap.Api.Azure.Auth` | Azure authentication for integration tests |
| `CasCap.Common.Configuration` | `AddStandardConfiguration` / `AddKeyVaultConfigurationFrom` |
| `CasCap.Common.Extensions` | Shared extension helpers |
| `CasCap.Common.Logging` | xUnit logging integration |
| `CasCap.Common.Net` | HTTP helpers |
| `CasCap.Common.Testing` | `AddXUnitLogging` and test utilities |


## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
