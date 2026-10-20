# CasCap.Api.Buderus.Sinks

Storage sinks for [CasCap.Api.Buderus](https://github.com/f2calv/SmartHaus/tree/main/src/CasCap.Api.Buderus).
They persist `BuderusEvent` readings to Redis or Azure Tables according to the feature's `SinkConfig`.

## Installation

Targets .NET 8 and .NET 10; includes the core Buderus library.

```bash
dotnet add package CasCap.Api.Buderus.Sinks
```

## Usage

```csharp
using CasCap.Extensions;

builder.Services.AddBuderusWithExtraSinks(builder.Configuration);
```

`builder` is your .NET host builder. Load the complete
[Buderus device configuration](https://github.com/f2calv/SmartHaus/tree/main/src/CasCap.Api.Buderus#configuration-examples)
plus the sink fragment below. The host must register Redis caching ([`IRemoteCache`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Caching/Abstractions/IRemoteCache.cs)) or Azure
authentication/storage dependencies for the selected backend and run the feature workers.
Use `lite: true` for a query-only process; it excludes Azure Tables and leaves Redis available.

## Sinks

This project provides additional [`IEventSink<BuderusEvent>`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Abstractions/Abstractions/IEventSink%7BT%7D.cs) implementations beyond the Console,
Memory, and Metrics sinks that ship with `CasCap.Api.Buderus`. The sink assembly is scanned by
`AddBuderusWithExtraSinks()` at startup, and only sinks whose `SinkTypeAttribute` name is
`Enabled = true` in `BuderusConfig.Sinks` are registered.

| Sink class | `SinkType` | Description |
| --- | --- | --- |
| `BuderusSinkRedisService` | `"Redis"` | Stores the latest value for each datapoint in a Redis hash (`SinkSettingKeys.SnapshotValues`). Maintains a per-datapoint sorted set of historical events for time-range queries. Also implements [`IBuderusQuery`](https://github.com/f2calv/SmartHaus/blob/main/src/CasCap.Api.Buderus/Abstractions/IBuderusQuery.cs) for snapshot retrieval. |
| `BuderusSinkAzureTablesService` | `"AzureTables"` | Writes individual `BuderusEvent` rows to a line-items Azure Table and upserts a single rolling snapshot row where each datapoint ID is a column. |

## Configuration Examples

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
              "SnapshotValues": "buderus:snapshot:values",
              "SeriesValues": "buderus:series"
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

This project is released under the [MIT License](https://github.com/f2calv/SmartHaus/blob/main/LICENSE).
