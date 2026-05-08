# CasCap.Api.DoorBird.Sinks

Pluggable event sink implementations for the DoorBird door station integration library ([CasCap.Api.DoorBird](../CasCap.Api.DoorBird)). Sinks persist `DoorBirdEvent` instances to external storage backends and are loaded based on the feature's `SinkConfig`.

## Installation

```bash
dotnet add package CasCap.Api.DoorBird.Sinks
```

## Purpose

This project provides additional `IEventSink<DoorBirdEvent>` implementations beyond the default in-memory and Console sinks that ship with `CasCap.Api.DoorBird`. The sink assembly is scanned by `AddDoorBirdWithExtraSinks()` at startup, and only sinks whose `SinkTypeAttribute` name is `Enabled = true` in `DoorBirdConfig.Sinks` are registered.

### Sinks

| Sink class | `SinkType` | Description |
| --- | --- | --- |
| `DoorBirdSinkRedisService` | `"Redis"` | Maintains running event counts and last-seen timestamps per event type (doorbell, motion, RFID, relay) in a Redis hash. Also implements `IDoorBirdQuery`. |
| `DoorBirdSinkAzTablesService` | `"AzureTables"` | Writes individual `DoorBirdEvent` rows to a line-items Azure Table and upserts a single snapshot row with aggregate counts and timestamps. |
| `DoorBirdSinkAzBlobStorageService` | `"AzureBlob"` | Enqueues captured JPEG image bytes to `BlobStatics.UploadQueue` for asynchronous upload to Azure Blob Storage by `BlobProcessorBgService`. |
| `BlobProcessorBgService` | — | Background service that reads from `BlobStatics.UploadQueue` and uploads each JPEG blob to Azure Blob Storage via `IDoorBirdAzBlobStorageService`. |

## Configuration

Sinks are enabled in `DoorBirdConfig.Sinks.AvailableSinks`. Example `appsettings.json` fragment:

```json
{
  "CasCap": {
    "DoorBirdConfig": {
      "Sinks": {
        "AvailableSinks": {
          "Redis": {
            "Enabled": true,
            "Settings": {
              "SnapshotValues": "doorbird:snapshot"
            }
          },
          "AzureTables": { "Enabled": true },
          "AzureBlob": { "Enabled": true },
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
| `CasCap.Api.DoorBird` | Core DoorBird integration library, `DoorBirdEvent`, `DoorBirdConfig` |
| `CasCap.Api.Azure.Auth` | Azure authentication and `AzureAuthConfig` for Storage credential resolution |
| `CasCap.Api.Azure.Storage` | Azure Table/Blob Storage entity types and base services |
| `CasCap.Common.Caching` | Redis caching abstractions |

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
