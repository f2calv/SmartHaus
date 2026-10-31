# CasCap.Api.DoorBird.Sinks

Storage sinks for [CasCap.Api.DoorBird](https://github.com/f2calv/SmartHaus/tree/main/src/CasCap.Api.DoorBird).
They persist `DoorBirdEvent` data and captured images according to the feature's `SinkConfig`.

## Installation

Targets .NET 8 and .NET 10; includes the core DoorBird library.

```bash
dotnet add package CasCap.Api.DoorBird.Sinks
```

## Usage

```csharp
using CasCap.Extensions;

builder.Services.AddDoorBirdWithExtraSinks(builder.Configuration);
```

`builder` is your application host builder. Merge the fragment below with the complete
[DoorBird configuration](https://github.com/f2calv/SmartHaus/tree/main/src/CasCap.Api.DoorBird#configuration-examples).
Register Redis caching or Azure credentials/storage services for the enabled sinks. Azure Blob
capture also needs the upload processor's host lifecycle. `lite: true` excludes Azure Tables, not
Redis, and omits the core device worker.

## Sinks

This project provides additional [`IEventSink<DoorBirdEvent>`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Abstractions/Abstractions/IEventSink%7BT%7D.cs) implementations beyond the Console,
Memory, and Metrics sinks that ship with `CasCap.Api.DoorBird`. The sink assembly is scanned by
`AddDoorBirdWithExtraSinks()` at startup, and only sinks whose `SinkTypeAttribute` name is
`Enabled = true` in `DoorBirdConfig.Sinks` are registered.

| Sink class | `SinkType` | Description |
| --- | --- | --- |
| `DoorBirdSinkRedisService` | `"Redis"` | Maintains running event counts and last-seen timestamps per event type (doorbell, motion, RFID, relay) in a Redis hash. Also implements [`IDoorBirdQuery`](https://github.com/f2calv/SmartHaus/blob/main/src/CasCap.Api.DoorBird/Abstractions/IDoorBirdQuery.cs). |
| `DoorBirdSinkAzureTablesService` | `"AzureTables"` | Writes individual `DoorBirdEvent` rows to a line-items Azure Table and upserts a single snapshot row with aggregate counts and timestamps. |
| `DoorBirdSinkAzBlobService` | `"AzureBlob"` | Enqueues captured JPEG image bytes to `BlobStatics.UploadQueue` for asynchronous upload to Azure Blob Storage by `BlobProcessorBgService`. |
| `BlobProcessorBgService` | — | Supervised `IBgFeature` that reads from `BlobStatics.UploadQueue` and uploads each JPEG blob to Azure Blob Storage via [`IDoorBirdAzBlobStorageService`](https://github.com/f2calv/SmartHaus/blob/main/src/CasCap.Api.DoorBird.Sinks/Abstractions/IDoorBirdAzBlobStorageService.cs). |

## Configuration Examples

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
              "SnapshotValues": "doorbird:snapshot:values",
              "SeriesValues": "doorbird:series"
            }
          },
          "AzureTables": { "Enabled": true },
          "AzureBlob": { "Enabled": true }
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

This project is released under the [MIT License](https://github.com/f2calv/SmartHaus/blob/main/LICENSE).
