# CasCap.Api.Shelly

Shelly smart plug integration using the Shelly Cloud REST API.

## Purpose

Provides background monitoring and relay control for Shelly Plug S (Gen1) devices via the Shelly Cloud API. Polls device status at a configurable interval and publishes events to the standard sink architecture.

## Services

| Service | Description |
| --- | --- |
| `ShellyCloudClientService` | HTTP client wrapping the Shelly Cloud REST API (`/device/status`, `/device/relay/control`) |
| `ShellyMonitorBgService` | Background polling service — reads device status and fans out to all `IEventSink<ShellyEvent>` |
| `ShellyQueryService` | Query facade — delegates to the Cloud API for live data and the primary sink for historical data |
| `ShellyController` | REST API controller — snapshot, readings, config, status, and relay on/off |

## Configuration

Configuration is bound from `CasCap:ShellyConfig` in `appsettings.json`.

| Property | Description |
| --- | --- |
| `BaseAddress` | Shelly Cloud API server URL (e.g. `https://shelly-xx-eu.shelly.cloud`) |
| `AuthKey` | Cloud API authentication key from the Shelly Cloud app |
| `DeviceId` | Device ID of the Shelly Plug S |
| `Channel` | Relay channel index (default `0`) |
| `PollingIntervalMs` | Status polling interval in milliseconds (default `30000`) |

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

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
