# CasCap.Api.Knx.Tests

Offline leadership/broker/shutter tests and configuration/storage integration tests for
[CasCap.Api.Knx](../CasCap.Api.Knx/README.md) and its
[storage sinks](../CasCap.Api.Knx.Sinks/README.md).

## Purpose

These tests verify KNX group address parsing, ETS metadata loading, and CEMI frame decoding. They run against a real KNX configuration file (`knxgroupaddresses.xml`) and optionally a live Azure Table Storage instance.

## Tests

Source-declared counts include skipped cases and expand inline theories; no execution is implied.

| Class | Methods | Expanded cases | Categories |
| --- | ---: | ---: | --- |
| `GroupAddressTests` | 6 | 6 | `Integration` |
| `CemiDecodingTests` | 1 | 1 | `Integration` |
| `ChannelKnxTelegramBrokerTests` | 4 | 4 | None |
| `KnxShutterMovementTests` | 6 | 12 | None |
| `KnxLeadershipTests` | 11 | 18 | `Leadership` |
| Total | 28 | 41 | |

Declared trait categories are `Integration` and `Leadership`. Broker and shutter tests have no category trait.

Leadership cases use synthetic ETS XML, fake runners/monitor sessions and in-memory brokers. They
verify standby behavior, routing/sharding selection, fresh ownership sessions, cancellation,
publication failure, late-telegram rejection, standby HTTP-command forwarding, awaited queue work,
ownership-bound day/night handover/cadence, per-write recovery and host-stop signalling before gated cleanup. The
scheduler clock is stepped without wall-clock waits. These cases do not contact Redis, discover
interfaces, send physical telegrams, exercise Falcon disposal bounds or prove live failover.

## Skipped Tests

| Test | Reason | Cases |
| --- | --- | ---: |
| `GroupAddressTests.ParseGroupAddressInt` | unfinished | 1 |

Other integration cases do not automatically skip when configuration or storage is missing.

## Test Layout

```text
CasCap.Api.Knx.Tests/
├── ChannelKnxTelegramBrokerTests.cs
├── CemiDecodingTests.cs
├── GroupAddressTests.cs
├── KnxShutterMovementTests.cs
├── TestBase.cs
└── Tests/
    └── Unit/
        ├── KnxLeadershipTests.cs
        └── KnxTestClock.cs
```

Older tests live at the project root; new leadership tests use `Tests/Unit/`.

## Prerequisites

- Standard configuration with `AppConfig`, connection settings and `CasCap:KnxConfig`.
- Private overrides supplied through local configuration, user secrets or environment variables.
- `knxgroupaddresses.xml` accessible via `GroupAddressXmlFilePath` in the `Test` configuration chain;
  use optional `appsettings.Local.Test.json`, User Secrets or environment variables for private values.
- Azure Table Storage and access to an appropriate captured-frame dataset for `CemiDecodingTests`.
- `TestBase` loads standard configuration and optional Key Vault through `AzureAuthConfig`.

## Running the tests

```bash
# Offline broker and shutter cases
dotnet test --project src/CasCap.Api.Knx.Tests/CasCap.Api.Knx.Tests.csproj -c Debug --filter-not-trait "Category=Integration"
```

Use .NET 10, xUnit v3 and Microsoft.Testing.Platform from the repository root. Remove the filter
only with approval and the required ETS/storage setup.

## Dependencies

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.Api.Knx` | Library under test |
| `CasCap.Api.Knx.Sinks` | Sink implementations under test |
| `CasCap.App` | Shared configuration models (`ConnectionStrings`, `AppConfig`) |
| `CasCap.Common.Configuration` | `AddStandardConfiguration` / `AddKeyVaultConfigurationFrom` |
| `CasCap.Common.Extensions` | Shared extension helpers |
| `CasCap.Common.Logging` | xUnit logging integration |
| `CasCap.Common.Net` | HTTP helpers |
| `CasCap.Common.Testing` | `AddXUnitLogging` and test utilities |

## License

This project is released under the [MIT License](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
