# CasCap.Api.Shelly

Shelly Cloud REST API integration for .NET applications monitoring or controlling Plug S (Gen1)
devices. This source-only library targets .NET 10 and is not published as a NuGet package.

## Usage

Enable `Shelly` in SmartHaus's `CasCap:FeatureConfig:EnabledFeatures`, or register it in your host:

```csharp
using CasCap.Extensions;
using CasCap.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddShelly(builder.Configuration, lite: true);
using var host = builder.Build();
var status = await host.Services.GetRequiredService<ShellyCloudClientService>()
    .GetDeviceStatus("example-device");
```

Load the JSON below through configuration and replace the synthetic device ID before use.
`lite: true` omits monitoring. Full monitoring needs the host's [`IBgFeature`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Abstractions/Abstractions/IBgFeature.cs) runner and health checks.
`SetRelayState` changes the connected device's power state.

## Purpose

Provides background monitoring and relay control for Shelly Plug S (Gen1) devices via the Shelly Cloud API. Polls device status at a configurable interval and publishes events to the standard sink architecture.

## Services

| Service | Description |
| --- | --- |
| `ShellyCloudClientService` | HTTP client wrapping the Shelly Cloud REST API (`/device/status`, `/device/relay/control`) |
| `ShellyMonitorBgService` | Background polling service — reads device status and fans out to all [`IEventSink<ShellyEvent>`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Abstractions/Abstractions/IEventSink%7BT%7D.cs) |
| `ShellyQueryService` | Query facade — delegates to the Cloud API for live data and the primary sink for historical data |
| `ShellyController` | REST API controller — snapshot, readings, config, status, and relay on/off |

## Configuration

Configuration is bound from `CasCap:ShellyConfig` in `appsettings.json`.

| Property | Description |
| --- | --- |
| `BaseAddress` | Shelly Cloud API server URL (e.g. `https://shelly-xx-eu.shelly.cloud`) |
| `AuthKey` | Cloud API authentication key from the Shelly Cloud app |
| `Devices` | Required array of `DeviceId`, `DeviceName` and optional `Channel` (default `0`) |
| `PollingIntervalMs` | Status polling interval in milliseconds (default `30000`) |
| `AzureTableStorageConnectionString` | Required by options validation, even with only local sinks |
| `Sinks` | Required sink selection: Console, Memory, Metrics, or [extra storage sinks](../CasCap.Api.Shelly.Sinks/README.md) |

## Configuration Examples

### Minimal

```json
{
  "CasCap": {
    "ShellyConfig": {
      "BaseAddress": "https://shelly.example.com",
      "AuthKey": "<cloud-auth-key>",
      "Devices": [{ "DeviceId": "example-device", "DeviceName": "Example plug" }],
      "AzureTableStorageConnectionString": "https://example.table.core.windows.net",
      "Sinks": { "AvailableSinks": { "Memory": { "Enabled": true } } }
    }
  }
}
```

### Multiple devices and metrics

```json
{
  "CasCap": {
    "ShellyConfig": {
      "BaseAddress": "https://shelly.example.com",
      "AuthKey": "<cloud-auth-key>",
      "Devices": [
        { "DeviceId": "example-device-1", "DeviceName": "Example plug 1", "Channel": 0 },
        { "DeviceId": "example-device-2", "DeviceName": "Example plug 2", "Channel": 0 }
      ],
      "PollingIntervalMs": 30000,
      "AzureTableStorageConnectionString": "https://example.table.core.windows.net",
      "Sinks": {
        "AvailableSinks": {
          "Memory": { "Enabled": true },
          "Metrics": { "Enabled": true }
        }
      }
    }
  }
}
```

## Dependencies

### NuGet Packages

| Package | Purpose |
| --- | --- |
| [Asp.Versioning.Mvc](https://www.nuget.org/packages/asp.versioning.mvc) | API versioning |
| [Microsoft.AspNetCore.Mvc.Core](https://www.nuget.org/packages/microsoft.aspnetcore.mvc.core) | MVC core for REST controllers |
| [Microsoft.Extensions.Diagnostics.HealthChecks](https://www.nuget.org/packages/microsoft.extensions.diagnostics.healthchecks) | Health check abstractions |
| [Microsoft.Extensions.Http](https://www.nuget.org/packages/microsoft.extensions.http) | `HttpClient` factory |
| [Microsoft.Extensions.Http.Resilience](https://www.nuget.org/packages/microsoft.extensions.http.resilience) | HTTP resilience policies |

### Project References

| Reference | Purpose |
| --- | --- |
| `CasCap.Common.Configuration` | Configuration binding and validation |
| `CasCap.Common.Extensions` | Common extension methods and sink infrastructure |
| `CasCap.Common.Logging` | Structured logging |
| `CasCap.Common.Net` | HTTP client base classes |
| `CasCap.Common.Extensions.Diagnostics.HealthChecks` | Health check base classes |
| `CasCap.Common.Serialization.Json` | JSON serialization |

## License

This project is released under the [MIT License](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
