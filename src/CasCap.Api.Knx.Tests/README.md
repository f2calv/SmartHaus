# CasCap.Api.Knx.Tests

Offline broker/shutter tests and configuration/storage integration tests for
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
| Total | 17 | 23 | |

`Integration` is the only declared trait category. Broker and shutter tests have no category trait.

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
└── TestBase.cs
```

Tests currently live at the project root rather than under `Tests/`.

## Prerequisites

- Standard configuration with `AppConfig`, connection settings and `CasCap:KnxConfig`.
- Private overrides supplied through local configuration, user secrets or environment variables.
- `knxgroupaddresses.xml` accessible via the `GroupAddressXmlFilePath` configured in `appsettings.Development.json`.
- Azure Table Storage and access to an appropriate captured-frame dataset for `CemiDecodingTests`.
- `TestBase` loads standard configuration and optional Key Vault through `AzureAuthConfig`.

## Running the tests

```bash
# Offline broker and shutter cases
dotnet test --project src/CasCap.Api.Knx.Tests/CasCap.Api.Knx.Tests.csproj -- --filter-not-trait "Category=Integration"
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
