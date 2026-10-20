# CasCap.Api.Miele.Tests

Integration tests for the Miele appliance library ([CasCap.Api.Miele](../CasCap.Api.Miele)) exercising `MieleClientService` against the live Miele 3rd Party Cloud API.

## Purpose

These tests query the Miele Cloud API using a caller-supplied OAuth token. They do not implement
token acquisition or renewal, and all seven cases are currently statically skipped.

## Tests

| Class | Methods | Expanded cases | Categories |
| --- | ---: | ---: | --- |
| `MieleClientServiceTests` | 7 | 7 | `Integration` |

Counts describe source declarations, not a successful live run.

## Skipped Tests

| Reason | Cases |
| --- | ---: |
| Requires a valid OAuth token | 2 |
| Requires a valid OAuth token and device ID | 5 |

Supplying credentials does not remove these explicit `[Fact(Skip = ...)]` attributes.

## Test Layout

```text
CasCap.Api.Miele.Tests/
├── MieleClientServiceTests.cs
└── TestBase.cs
```

Tests currently live at the project root rather than under `Tests/`.

## Prerequisites

- A Miele account with at least one registered appliance.
- Standard configuration with `CasCap:MieleConfig:OAuthToken` and the required sink options.
- Credentials supplied by local/user-secret/environment providers, never tracked JSON.
- `TestBase` loads standard configuration and optional Key Vault through `AzureAuthConfig`.

## Running the tests

```bash
dotnet test --project src/CasCap.Api.Miele.Tests/CasCap.Api.Miele.Tests.csproj
```

Run from the repository root with .NET 10, xUnit v3 and Microsoft.Testing.Platform. The command
discovers skipped cases; it is not evidence of cloud connectivity.

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

This project is released under the [MIT License](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
