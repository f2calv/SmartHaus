# CasCap.Api.Miele.Sinks

Pluggable event sink implementations for the Miele appliance integration library ([CasCap.Api.Miele](../CasCap.Api.Miele)). Sinks persist `MieleEvent` instances to external storage backends and are loaded based on the feature's `SinkConfig`.

## Purpose

This project provides additional [`IEventSink<T>`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Abstractions/Abstractions/IEventSink%7BT%7D.cs) implementations for the Miele integration. Sinks are discovered at runtime via `SinkTypeAttribute` and loaded based on the feature's `SinkConfig` when `AddMieleWithExtraSinks()` is called.

This source-only library targets .NET 10 and is not published as a NuGet package.
`MieleSinkRedisService` stores per-device snapshots and daily series; `MieleSinkAzureTablesService`
persists event rows and snapshots.

## Usage

```csharp
using CasCap.Extensions;

builder.Services.AddMieleWithExtraSinks(builder.Configuration);
```

`builder` is your host builder. Supply the complete [Miele configuration](../CasCap.Api.Miele/README.md),
Redis caching or Azure credentials/storage dependencies, and the host's feature runner.
`lite: true` excludes Azure Tables from the extra-sink scan but still invokes core Miele
registration; it does not suppress the core SSE worker registration.

## Configuration Examples

Merge this sink fragment with the core OAuth settings:

```json
{
  "CasCap": {
    "MieleConfig": {
      "Sinks": {
        "AvailableSinks": {
          "Redis": {
            "Enabled": true,
            "Settings": {
              "SnapshotValues": "miele:snapshot:values",
              "SeriesValues": "miele:series"
            }
          }
        }
      }
    }
  }
}
```

For Azure persistence, also enable `AzureTables` and configure
`MieleConfig.AzureTableStorageConnectionString` and authentication.

## Dependencies

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.Api.Miele` | Core Miele appliance integration library |
| `CasCap.Api.Azure.Auth` | Azure authentication and `AzureAuthConfig` for Storage credential resolution |
| `CasCap.Api.Azure.Storage` | Azure Table Storage entity types and base services |
| `CasCap.Common.Caching` | Redis caching abstractions |

## License

This project is released under the [MIT License](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
