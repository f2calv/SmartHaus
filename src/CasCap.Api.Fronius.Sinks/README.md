# CasCap.Api.Fronius.Sinks

Pluggable event sink implementations for the Fronius solar inverter integration library ([CasCap.Api.Fronius](../CasCap.Api.Fronius)). Sinks persist `FroniusEvent` instances to external storage backends and are loaded based on the feature's `SinkConfig`.

## Installation

```bash
dotnet add package CasCap.Api.Fronius.Sinks
```

## Purpose

This project provides additional `IEventSink<FroniusEvent>` implementations beyond the default in-memory and Console sinks that ship with `CasCap.Api.Fronius`. The sink assembly is scanned by `AddFroniusWithExtraSinks()` at startup, and only sinks whose `SinkTypeAttribute` name is `Enabled = true` in `FroniusConfig.Sinks` are registered.

### Sinks

| Sink class | `SinkType` | Description |
| --- | --- | --- |
| `FroniusSinkRedisService` | `"Redis"` | Stores the latest five power metrics (`SOC`, `P_Akku`, `P_Grid`, `P_Load`, `P_PV`) in a Redis hash. Also implements `IFroniusQuery` for snapshot retrieval. |
| `FroniusSinkAzTablesService` | `"AzureTables"` | Writes detailed `FroniusEvent` rows to a line-items Azure Table and upserts a rolling snapshot row per inverter. |

## Configuration

Sinks are enabled in `FroniusConfig.Sinks.AvailableSinks`. Example `appsettings.json` fragment:

```json
{
  "CasCap": {
    "FroniusConfig": {
      "Sinks": {
        "AvailableSinks": {
          "Redis": {
            "Enabled": true,
            "Settings": {
              "SnapshotValues": "fronius:snapshot"
            }
          },
          "AzureTables": { "Enabled": true }
        }
      }
    }
  }
}
```

## Dependencies

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.Api.Fronius` | Core Fronius integration library, `FroniusEvent`, `FroniusConfig` |
| `CasCap.Api.Azure.Auth` | Azure authentication and `AzureAuthConfig` for Table Storage credential resolution |
| `CasCap.Api.Azure.Storage` | Azure Table Storage entity types and base services |
| `CasCap.Common.Caching` | Redis caching abstractions |

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
