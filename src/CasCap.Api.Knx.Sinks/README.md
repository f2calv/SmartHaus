# CasCap.Api.Knx.Sinks

Storage sinks for [CasCap.Api.Knx](https://github.com/f2calv/SmartHaus/tree/main/src/CasCap.Api.Knx).
They persist decoded `KnxEvent` data and raw CEMI frames according to the feature's `SinkConfig`.

## Installation

Targets .NET 8 and .NET 10; includes the core KNX library.

```bash
dotnet add package CasCap.Api.Knx.Sinks
```

## Usage

```csharp
using CasCap.Extensions;

builder.Services.AddKnxWithExtraSinks(builder.Configuration);
```

`builder` is your .NET host builder. Merge this fragment with the complete
[KNX configuration](https://github.com/f2calv/SmartHaus/tree/main/src/CasCap.Api.Knx#configuration-examples).
The host supplies Redis/RedLock, Azure credentials where selected, and the feature-worker runner.
`lite: true` omits bus workers and excludes the Redis event sink from this assembly scan.
The core registration can still use `KnxRedisStateService` when Redis is configured; Azure Tables
sinks are not filtered out by this extension. This differs from other integrations' lite policy.

## Sinks

This project provides additional [`IEventSink<KnxEvent>`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Abstractions/Abstractions/IEventSink%7BT%7D.cs) implementations beyond the Console and
Metrics sinks that ship with `CasCap.Api.Knx`. The sink assembly is scanned by
`AddKnxWithExtraSinks()` at startup, and only sinks whose `SinkTypeAttribute` name is
`Enabled = true` in `KnxConfig.Sinks` are registered.

| Sink class | `SinkType` | Description |
| --- | --- | --- |
| `KnxSinkRedisService` | `"Redis"` | Writes decoded values through [`IKnxState`](https://github.com/f2calv/SmartHaus/blob/main/src/CasCap.Api.Knx/Abstractions/IKnxState.cs) and optional daily/per-address sorted-set series. `GetEvents` returns current state, not historical series. |
| `KnxSinkAzureTablesService` | `"AzureTables"` | Writes individual `KnxEvent` rows to a line-items Azure Table and maintains a rolling snapshot table where each group address is a column. |
| `KnxSinkAzureTablesCemiService` | `"AzureTablesCemi"` | Batches raw CEMI L-Data frames to a separate Azure Table for low-level protocol analysis and replay. |

## Configuration Examples

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
              "SeriesValues": "knx:series"
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

This project is released under the [MIT License](https://github.com/f2calv/SmartHaus/blob/main/LICENSE).
