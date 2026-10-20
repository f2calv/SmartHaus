# CasCap.Api.Sicce.Sinks

Pluggable event sink implementations for the Sicce smart water pump integration library ([CasCap.Api.Sicce](../CasCap.Api.Sicce)). Sinks persist `SicceEvent` instances to external storage backends and are loaded based on the feature's `SinkConfig`.

## Purpose

This source-only library targets .NET 10; it is not published as a NuGet package.

## Usage

```csharp
using CasCap.Extensions;

builder.Services.AddSicceWithExtraSinks(builder.Configuration);
```

`builder` is your application host builder. Merge the fragment below with the
[Sicce configuration](../CasCap.Api.Sicce/README.md#configuration-examples).
The host supplies Redis caching or Azure credentials/storage services and the feature runner.
`lite: true` omits monitoring and Azure Tables but retains Redis queries.

## Sink Selection

This project provides additional [`IEventSink<SicceEvent>`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Abstractions/Abstractions/IEventSink%7BT%7D.cs) implementations beyond the default in-memory and Console sinks that ship with `CasCap.Api.Sicce`. The sink assembly is scanned by `AddSicceWithExtraSinks()` at startup, and only sinks whose `SinkTypeAttribute` name is `Enabled = true` in `SicceConfig.Sinks` are registered.

### Sinks

| Sink class | `SinkType` | Description |
| --- | --- | --- |
| `SicceSinkRedisService` | `"Redis"` | Stores temperature, power, online status, and power switch state in a Redis hash. Line items in daily sorted sets. Implements [`ISicceQuery`](https://github.com/f2calv/SmartHaus/blob/main/src/CasCap.Api.Sicce/Abstractions/ISicceQuery.cs) for snapshot retrieval. |
| `SicceSinkAzureTablesService` | `"AzureTables"` | Writes detailed `SicceEvent` rows to a line-items Azure Table and upserts a rolling snapshot row. Implements `ISicceQuery`. |

## Configuration Examples

Sinks are enabled in `SicceConfig.Sinks.AvailableSinks`. Example `appsettings.json` fragment:

```json
{
  "CasCap": {
    "SicceConfig": {
      "Sinks": {
        "AvailableSinks": {
          "Redis": {
            "Enabled": true,
            "Settings": {
              "SnapshotValues": "sicce:snapshot:values",
              "SeriesValues": "sicce:series"
            }
          },
          "AzureTables": {
            "Enabled": true,
            "Settings": {
              "LineItemTableName": "siccelineitemsv1",
              "SnapshotTableName": "siccesnapshotv1"
            }
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
| `CasCap.Api.Sicce` | Core Sicce water pump integration library |
| `CasCap.Api.Azure.Auth` | Azure authentication and `AzureAuthConfig` for Table Storage credential resolution |
| `CasCap.Api.Azure.Storage` | Azure Table Storage entity types and base services |
| `CasCap.Common.Caching` | Redis caching abstractions |

## License

This project is released under the [MIT License](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
