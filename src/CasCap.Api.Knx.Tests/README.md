# CasCap.Api.Knx.Tests

Integration tests for the KNX building automation library ([CasCap.Api.Knx](../CasCap.Api.Knx)) and its sinks ([CasCap.Api.Knx.Sinks](../CasCap.Api.Knx.Sinks)).

## Purpose

These tests verify KNX group address parsing, ETS metadata loading, and CEMI frame decoding. They run against a real KNX configuration file (`knxgroupaddresses.xml`) and optionally a live Azure Table Storage instance.

### Test classes

| Class | Description |
| --- | --- |
| `GroupAddressTests` | Unit and integration tests for group address parsing, ETS naming-convention validation, Unix timestamp conversions, and group-address lookup |
| `CemiDecodingTests` | Integration tests that retrieve raw CEMI frames from Azure Table Storage via `KnxSinkCemiAzTablesService` and verify the full deserialization and `GroupValue` decoding pipeline |
| `ChannelKnxTelegramBrokerTests` | Unit tests for `ChannelKnxTelegramBroker<T>` verifying publish/subscribe round-tripping and item ordering |

## Prerequisites

- `appsettings.json` with `AppConfig`, `ConnectionStrings`, and `KnxConfig` sections.
- `appsettings.Development.json` (optional) with secrets and environment-specific overrides.
- `knxgroupaddresses.xml` accessible via the `GroupAddressXmlFilePath` configured in `appsettings.Development.json`.
- Azure Table Storage connection string populated in `ConnectionStrings.AzureStorageBlob` for `CemiDecodingTests` (requires access to production data).

## Running the tests

```bash
dotnet test src/CasCap.Api.Knx.Tests/CasCap.Api.Knx.Tests.csproj
```

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

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
