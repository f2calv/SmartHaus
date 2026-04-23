# CasCap.Api.Buderus.Sinks

Pluggable event sink implementations for the Buderus heating system integration library ([CasCap.Api.Buderus](../CasCap.Api.Buderus)). Sinks persist `BuderusEvent` instances to external storage backends and are loaded based on the feature's `SinkConfig`.

## Purpose

This project provides additional `IEventSink<BuderusEvent>` implementations beyond the default in-memory and Console sinks that ship with `CasCap.Api.Buderus`. The sink assembly is scanned by `AddBuderusWithExtraSinks()` at startup, and only sinks whose `SinkTypeAttribute` name is `Enabled = true` in `BuderusConfig.Sinks` are registered.

### Sinks

| Sink class | `SinkType` | Description |
| --- | --- | --- |
| `BuderusSinkRedisService` | `"Redis"` | Stores the latest value for each datapoint in a Redis hash (`SinkSettingKeys.SnapshotValues`). Maintains a per-datapoint sorted set of historical events for time-range queries. Also implements `IBuderusQuery` for snapshot retrieval. |
| `BuderusSinkAzTablesService` | `"AzureTables"` | Writes individual `BuderusEvent` rows to a line-items Azure Table and upserts a single rolling snapshot row where each datapoint ID is a column. |

## Configuration

Sinks are enabled in `BuderusConfig.Sinks.AvailableSinks`. Example `appsettings.json` fragment:

```json
{
  "CasCap": {
    "BuderusConfig": {
      "Sinks": {
        "AvailableSinks": {
          "Redis": {
            "Enabled": true,
            "Settings": {
              "SnapshotValues": "buderus:snapshot"
            }
          },
          "AzureTables": {
            "Enabled": true
          }
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
| `CasCap.Api.Buderus` | Core Buderus integration library, `BuderusEvent`, `BuderusConfig` |
| `CasCap.Api.Azure.Auth` | Azure authentication and `AzureAuthConfig` for Table Storage credential resolution |
| `CasCap.Api.Azure.Storage` | Azure Table Storage entity types and base services |
| `CasCap.Common.Caching` | Redis caching abstractions |

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
