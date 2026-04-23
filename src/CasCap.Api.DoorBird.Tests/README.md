# CasCap.Api.DoorBird.Tests

Integration tests for the DoorBird door station library ([CasCap.Api.DoorBird](../CasCap.Api.DoorBird)) exercising `DoorBirdClientService` against a real DoorBird device.

## Purpose

These tests verify that the DoorBird LAN API can be reached and that device commands (session, info, snapshot) return well-formed responses.

### Test classes

| Class | Description |
| --- | --- |
| `DoorBirdClientServiceTests` | Integration tests for device info retrieval, session management, photo snapshot, and event history queries |

## Prerequisites

- A DoorBird device accessible on the local network from the test host.
- `appsettings.json` with `AppConfig`, `ConnectionStrings`, and `CasCap:DoorBirdConfig` sections.
- `appsettings.Development.json` (optional) with the device's local IP address, username, and password.

## Running the tests

```bash
dotnet test src/CasCap.Api.DoorBird.Tests/CasCap.Api.DoorBird.Tests.csproj
```

## Dependencies

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.Api.DoorBird` | Library under test |
| `CasCap.Api.Azure.Auth` | Azure authentication for integration tests |
| `CasCap.App` | Shared configuration models (`ConnectionStrings`, `AppConfig`) |
| `CasCap.Common.Configuration` | `AddStandardConfiguration` / `AddKeyVaultConfigurationFrom` |
| `CasCap.Common.Extensions` | Shared extension helpers |
| `CasCap.Common.Logging` | xUnit logging integration |
| `CasCap.Common.Net` | HTTP helpers |
| `CasCap.Common.Testing` | `AddXUnitLogging` and test utilities |


## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
