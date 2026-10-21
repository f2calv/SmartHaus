# CasCap.Api.Shelly.Sinks

External storage sinks for the Shelly smart plug integration.

## Purpose

Provides Redis and Azure Table Storage sinks for persisting Shelly smart plug events outside the application process.

This source-only library targets .NET 10; it is not published as a NuGet package.

## Usage

```csharp
using CasCap.Extensions;

builder.Services.AddShellyWithExtraSinks(builder.Configuration);
```

`builder` is your application host builder. Merge the sink fragment below with the
[Shelly cloud/device configuration](../CasCap.Api.Shelly/README.md#configuration-examples).
The host supplies Redis caching or Azure authentication/storage dependencies and the feature
runner. `lite: true` omits monitoring and Azure Tables, retaining Redis-backed queries.

## Configuration Examples

```json
{
  "CasCap": {
    "ShellyConfig": {
      "Sinks": {
        "AvailableSinks": {
          "Redis": {
            "Enabled": true,
            "Settings": {
              "SnapshotValues": "shelly:snapshot:values",
              "SeriesValues": "shelly:series"
            }
          },
          "AzureTables": { "Enabled": true }
        }
      }
    }
  }
}
```

`SnapshotValues` is the prefix of per-device hashes; `SeriesValues` prefixes daily sorted sets.
This fragment does not replace the required cloud credentials and device array.

## Services

| Service | Description |
| --- | --- |
| `ShellySinkRedisService` | Persists snapshot as a Redis hash and line items as daily sorted sets |
| `ShellySinkAzureTablesService` | Persists line items and snapshot to Azure Table Storage |

## Dependencies

### Project References

| Reference | Purpose |
| --- | --- |
| `CasCap.Api.Shelly` | Core Shelly integration library |
| `CasCap.Api.Azure.Auth` | Azure authentication |
| `CasCap.Api.Azure.Storage` | Azure Table Storage client extensions |
| `CasCap.Common.Caching` | Redis remote cache abstraction |

## License

This project is released under the [MIT License](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
