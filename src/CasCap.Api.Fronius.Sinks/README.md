# CasCap.Api.Fronius.Sinks

Storage sinks for [CasCap.Api.Fronius](https://github.com/f2calv/SmartHaus/tree/main/src/CasCap.Api.Fronius).
They persist `FroniusEvent` solar readings according to the feature's `SinkConfig`.

## Installation

Targets .NET 8 and .NET 10; includes the core Fronius library.

```bash
dotnet add package CasCap.Api.Fronius.Sinks
```

## Usage

```csharp
using CasCap.Extensions;

builder.Services.AddFroniusWithExtraSinks(builder.Configuration);
```

`builder` is your .NET host builder. Merge this sink fragment into the complete
[Fronius configuration](https://github.com/f2calv/SmartHaus/tree/main/src/CasCap.Api.Fronius#configuration-examples).
The host supplies Redis caching or Azure credentials/storage services and the feature-worker
runner. `lite: true` omits monitoring and Azure Tables while retaining Redis queries.

## Sinks

This project provides additional [`IEventSink<FroniusEvent>`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Abstractions/Abstractions/IEventSink%7BT%7D.cs) implementations beyond the Console,
Memory, and Metrics sinks that ship with `CasCap.Api.Fronius`. The sink assembly is scanned by
`AddFroniusWithExtraSinks()` at startup, and only sinks whose `SinkTypeAttribute` name is
`Enabled = true` in `FroniusConfig.Sinks` are registered.

| Sink class | `SinkType` | Description |
| --- | --- | --- |
| `FroniusSinkRedisService` | `"Redis"` | Stores the latest five power metrics (`SOC`, `P_Akku`, `P_Grid`, `P_Load`, `P_PV`) in a Redis hash. Also implements [`IFroniusQuery`](https://github.com/f2calv/SmartHaus/blob/main/src/CasCap.Api.Fronius/Abstractions/IFroniusQuery.cs) for snapshot retrieval. |
| `FroniusSinkAzureTablesService` | `"AzureTables"` | Writes detailed `FroniusEvent` rows to a line-items Azure Table and upserts a rolling snapshot row per inverter. |

## Configuration Examples

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
              "SnapshotValues": "fronius:snapshot:values",
              "SeriesValues": "fronius:series"
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

This project is released under the [MIT License](https://github.com/f2calv/SmartHaus/blob/main/LICENSE).
