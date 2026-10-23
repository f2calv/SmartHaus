# CasCap.Api.Wiz.Sinks

Pluggable event sink implementations for the Wiz smart lighting integration library ([CasCap.Api.Wiz](../CasCap.Api.Wiz)).

## Purpose

This source-only .NET 10 library provides `WizSinkRedisService` and `WizSinkAzureTablesService`.
Redis stores per-device snapshots and daily series; Azure Tables persists event rows and snapshots.

## Usage

```csharp
using CasCap.Extensions;

builder.Services.AddWizWithExtraSinks(builder.Configuration);
```

`builder` is your application host builder. Merge the fragment below with the
[WiZ configuration](../CasCap.Api.Wiz/README.md#configuration-examples) and supply Redis caching or
Azure credentials/storage services. `lite: true` excludes Azure Tables from the sink scan but
does not suppress core discovery-worker registration.

These sinks consume `WizEvent` from the discovery worker when a bulb is first seen or its pilot
state changes. Discovery ignores MAC/RSSI-only changes when deciding whether to emit an event.

## Configuration Examples

```json
{
  "CasCap": {
    "WizConfig": {
      "Sinks": {
        "AvailableSinks": {
          "Redis": {
            "Enabled": true,
            "Settings": {
              "SnapshotValues": "wiz:snapshot:values",
              "SeriesValues": "wiz:series"
            }
          }
        }
      }
    }
  }
}
```

To add Azure persistence, enable `AzureTables`, set `WizConfig.AzureTableStorageConnectionString`
and configure authentication. Before discovery begins, the sink asynchronously creates its line-item
and snapshot tables if missing. The configured identity must have permission to create and write
those tables; initialization failures propagate instead of allowing discovery to run without persistence.

## Dependencies

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.Api.Wiz` | Core Wiz smart lighting integration library |
| `CasCap.Api.Azure.Auth` | Azure authentication and `AzureAuthConfig` for Storage credential resolution |
| `CasCap.Api.Azure.Storage` | Azure Table Storage entity types and base services |
| `CasCap.Common.Caching` | Redis caching abstractions |

## License

This project is released under the [MIT License](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
