# CasCap.Api.DoorBird.Tests

Unit and integration tests for the DoorBird door station library
([CasCap.Api.DoorBird](../CasCap.Api.DoorBird)), including bounded audio framing and live-device
LAN API behavior.

## Purpose

Unit tests validate protocol framing and bounds without credentials or network access. Integration
tests verify that the DoorBird LAN API can be reached and that device commands return well-formed
responses.

### Test classes

| Class | Methods | Cases | Category | Description |
| --- | ---: | ---: | --- | --- |
| `DoorBirdAudioCaptureTests` | 3 | 3 | Audio | Offline G.711 μ-law WAV framing, no-content, and byte-limit tests |
| `DoorBirdClientServiceTests` | 17 | 17 | Integration | Live device info, session, MJPEG video, microphone, relay, and history tests |

## Test Layout

```text
Tests/
├── Unit/
│   └── DoorBirdAudioCaptureTests.cs
└── Integration/
    ├── DoorBirdClientServiceTests.cs
    └── TestBase.cs
```

## Trait Categories

| Category | Purpose |
| --- | --- |
| `Audio` | Credential-free audio framing and capture-policy tests |
| `Integration` | Tests requiring a configured DoorBird device |

## Skipped Tests

No tests are permanently skipped. Integration tests require the prerequisites below and should be
selected explicitly only in an environment with a configured device.

## Prerequisites

- A DoorBird device accessible on the local network from the test host.
- `appsettings.json` with `AppConfig`, `ConnectionStrings`, and `CasCap:DoorBirdConfig` sections.
- `appsettings.Development.json` (optional) with the device's local IP address, username, and password.

## Running the tests

```bash
dotnet test --project src/CasCap.Api.DoorBird.Tests/CasCap.Api.DoorBird.Tests.csproj
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
