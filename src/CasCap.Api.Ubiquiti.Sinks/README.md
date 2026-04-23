# CasCap.Api.Ubiquiti.Sinks

Pluggable event sink implementations for the Ubiquiti UniFi Protect IP camera integration library ([CasCap.Api.Ubiquiti](../CasCap.Api.Ubiquiti)). Sinks persist `UbiquitiEvent` instances to external storage backends and are loaded based on the feature's `SinkConfig`.

## Purpose

This project provides additional `IEventSink<UbiquitiEvent>` implementations beyond the default in-memory and Console sinks that ship with `CasCap.Api.Ubiquiti`. The sink assembly is scanned by `AddUbiquitiWithExtraSinks()` at startup, and only sinks whose `SinkTypeAttribute` name is `Enabled = true` in `UbiquitiConfig.Sinks` are registered.

### Sinks

| Sink class | `SinkType` | Description |
| --- | --- | --- |
| `UbiquitiSinkRedisService` | `"Redis"` | Maintains running event counts and last-seen timestamps per event type (motion, smart detection, ring) in a Redis hash. Also implements `IUbiquitiQuery`. |
| `UbiquitiSinkAzTablesService` | `"AzureTables"` | Writes individual `UbiquitiEvent` rows to a line-items Azure Table and upserts a single snapshot row with aggregate counts and timestamps. |

## Configuration

Sinks are enabled in `UbiquitiConfig.Sinks.AvailableSinks`. Example `appsettings.json` fragment:

```json
{
  "CasCap": {
    "UbiquitiConfig": {
      "Sinks": {
        "AvailableSinks": {
          "Redis": {
            "Enabled": true,
            "Settings": {
              "SnapshotValues": "ubiquiti:snapshot",
              "SeriesValues": "ubiquiti:series"
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
| `CasCap.Api.Ubiquiti` | Core Ubiquiti integration library, `UbiquitiEvent`, `UbiquitiConfig` |
| `CasCap.Api.Azure.Auth` | Azure authentication and `AzureAuthConfig` for Storage credential resolution |
| `CasCap.Api.Azure.Storage` | Azure Table Storage entity types and base services |
| `CasCap.Common.Caching` | Redis caching abstractions |

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
