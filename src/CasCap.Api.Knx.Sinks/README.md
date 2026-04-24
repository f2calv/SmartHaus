# CasCap.Api.Knx.Sinks

Pluggable event sink implementations for the KNX building automation integration library ([CasCap.Api.Knx](../CasCap.Api.Knx)). Sinks persist `KnxEvent` instances to external storage backends and are loaded based on the feature's `SinkConfig`.

## Purpose

This project provides additional `IEventSink<KnxEvent>` implementations beyond the default in-memory, Console, Redis, Channel, OpenTelemetry, and gRPC sinks that ship with `CasCap.Api.Knx`. The sink assembly is scanned by `AddKnxWithExtraSinks()` at startup, and only sinks whose `SinkTypeAttribute` name is `Enabled = true` in `KnxConfig.Sinks` are registered.

### Sinks

| Sink class | `SinkType` | Description |
| --- | --- | --- |
| `KnxSinkRedisService` | `"Redis"` | Stores the latest decoded group-address value in a Redis hash and maintains per-group-address sorted sets of historical telegrams for time-range queries. Also implements `IKnxQuery`. |
| `KnxSinkAzTablesService` | `"AzureTables"` | Writes individual `KnxEvent` rows to a line-items Azure Table and maintains a rolling snapshot table where each group address is a column. |
| `KnxSinkCemiAzTablesService` | `"AzureTablesCemi"` | Batches raw CEMI L-Data frames to a separate Azure Table for low-level protocol analysis and replay. |

## Configuration

Sinks are enabled in `KnxConfig.Sinks.AvailableSinks`. Example `appsettings.json` fragment:

```json
{
  "CasCap": {
    "KnxConfig": {
      "Sinks": {
        "AvailableSinks": {
          "Redis": {
            "Enabled": true,
            "Settings": {
              "SnapshotValues": "knx:snapshot"
            }
          },
          "AzureTables": { "Enabled": true },
          "AzureTablesCemi": { "Enabled": true }
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
| `CasCap.Api.Knx` | Core KNX integration library, `KnxEvent`, `KnxConfig`, `IKnxState` |
| `CasCap.Api.Azure.Auth` | Azure authentication and `AzureAuthConfig` for Table Storage credential resolution |
| `CasCap.Api.Azure.Storage` | Azure Table Storage entity types and base services |
| `CasCap.Common.Caching` | Redis caching abstractions |

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
