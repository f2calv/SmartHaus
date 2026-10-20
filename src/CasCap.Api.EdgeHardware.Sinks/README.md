# CasCap.Api.EdgeHardware.Sinks

Pluggable event sink implementations for the edge hardware integration library ([CasCap.Api.EdgeHardware](../CasCap.Api.EdgeHardware)).

## Purpose

This source-only .NET 10 library provides `EdgeHardwareSinkRedisService` and
`EdgeHardwareSinkAzureTablesService`. Redis stores per-node snapshots and daily series; Azure
Tables stores event rows and snapshots. Sinks are selected through `EdgeHardwareConfig.Sinks`.

## Usage

```csharp
using CasCap.Extensions;

var gpuDetected = builder.Services.AddEdgeHardwareWithExtraSinks(
    builder.Configuration, cpuEnabled: true);
```

`builder` is your application host builder. Start the core hardware feature through the shared
feature runner and supply Redis caching or Azure authentication/storage services. `lite: true`
omits hardware polling/detection and Azure Tables while keeping Redis queries.

## Configuration Examples

Merge this fragment with the [hardware configuration](../CasCap.Api.EdgeHardware/README.md#configuration-examples).
`SnapshotValues` is a Redis key prefix, not a list of metric names.

```json
{
  "CasCap": {
    "EdgeHardwareConfig": {
      "Sinks": {
        "AvailableSinks": {
          "Redis": {
            "Enabled": true,
            "Settings": {
              "SnapshotValues": "edgehardware:snapshot:values",
              "SeriesValues": "edgehardware:series"
            }
          }
        }
      }
    }
  }
}
```

To add Azure persistence, enable `AzureTables` and provide the core configuration's storage
endpoint and credentials. This fragment does not replace required core options.

## Dependencies

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.Api.EdgeHardware` | Core edge hardware integration library |
| `CasCap.Api.Azure.Auth` | Azure authentication and `AzureAuthConfig` for Table Storage credential resolution |
| `CasCap.Api.Azure.Storage` | Azure Table Storage entity types and base services |
| `CasCap.Common.Caching` | Redis caching abstractions |

## License

This project is released under the [MIT License](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
