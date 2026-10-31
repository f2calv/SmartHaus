# CasCap.App.Server

The .NET 10 ASP.NET Core host for SmartHaus device integrations, versioned REST endpoints, SignalR
and stateless MCP tools. Communications call an external Agent Runtime; the server does not host
language-model agents.

## Quick Start

The [root demo](../../README.md#quick-start) describes the Compose entry point and its external
communications prerequisites. For a source checkout with the
[Debug dependencies](../../README.md#development) and local Redis/storage services configured:

```bash
dotnet run --project src/CasCap.App.Server --configuration Debug
```

Use the tracked development configuration as a reference, keep real values in ignored local
configuration, and select only features you intend to activate. Discover the bound HTTP address
from the startup output; `/swagger` provides API documentation.

## Purpose

`CasCap.App.Server` is the single deployable process that conditionally activates features at startup based on the `FeatureConfig.EnabledFeatures` comma-separated string. This enables the same binary to be deployed as different service profiles (e.g. KNX-only pod, full all-in-one instance).

`Program.cs` is a thin entry point. Application composition is split across `AppHost.cs`,
`AppHost.Features.cs`, `AppHost.WebApi.cs`, and `AppHost.Endpoints.cs`; configuration, logging,
caching, and telemetry ordering remains visible in the main `AppHost.RunAsync` lifecycle.

### Feature Flag Registration

Each feature name activates a distinct set of services at DI registration time. At runtime, the non-generic `FeatureFlagBgService` matches each [`IBgFeature.FeatureName`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Abstractions/Abstractions/IBgFeature.cs) string against the enabled features set:

| Flag | Services registered | MCP tools |
| --- | --- | --- |
| `Buderus` | `BuderusKm200MonitorBgService`, controller, sinks | `HeatPumpMcpQueryService` |
| `DoorBird` | Controller, sinks, callback endpoints | `FrontDoorMcpQueryService` |
| `Fronius` | `FroniusMonitorBgService`, controller, sinks | `InverterMcpQueryService` |
| `Knx` | `KnxMonitorBgService`, controller, sinks | `BusSystemMcpQueryService` |
| `EdgeHardware` | `EdgeHardwareMonitorBgService`, CPU/GPU sinks, GPIO sensors | `EdgeHardwareMcpQueryService` |
| `Sicce` | `SicceBgService` | — |
| `DDns` | `DDnsBgService` | — |
| `Miele` | SSE reader, controller, sinks | `AppliancesMcpQueryService` |
| `Shelly` | Cloud polling, controller, sinks | `SmartPlugMcpQueryService`, shared lighting tools |
| `Ubiquiti` | Webhook controller, sinks, optional camera clips | `IpCameraMcpQueryService` |
| `Wiz` | UDP discovery and state-change events | Shared lighting tools |
| `SignalRHub` | `HausHub` SignalR hub, hub sinks, Redis backplane | — |
| `Comms` | `CommunicationsBgService`, Signalizr client, comms stream sink; features also register in **lite mode** (sinks only, no polling) when this flag is set | — |

`Comms` also registers selected integrations in lite mode so its tools can query shared state;
disabled features are therefore not necessarily absent from DI. The MCP registry selects explicit
tool/prompt types by enabled feature rather than exposing the whole assembly.

### Always-On Services

The following services are registered regardless of feature flags:

| Service | Description |
| --- | --- |
| `ApplicationMetadataBgService` | Logs assembly-backed application metadata at startup and periodically (registered via `AddFeatureFlagService`) |
| `SystemController` | `GET /api/system` — returns assembly-backed `ApplicationMetadata` |

### Authentication

| Environment | Scheme | Behaviour |
| --- | --- | --- |
| Development | Basic auth | Default policy allows all requests (no credentials required) |
| Production | Basic auth | `BasicAuthenticationHandler` validates credentials from `ApiAuthConfig` |

### API Endpoints

All controllers are added dynamically based on `EnabledFeatures`. API versioning defaults to v1.

JSON endpoints use typed HTTP results. Enum values are written as strings, and controller JSON
input accepts string enum names. Invalid request bodies return HTTP 400 with
`application/problem+json` and a `ValidationProblemDetails` body containing an `errors` dictionary.
Consumers should read enum names rather than numeric values and use `errors` for field failures.
MVC input binding and typed-result output share this contract through their respective JSON options.
Shelly relay and Wiz power commands require an explicit `on=true` or `on=false` query value;
missing or invalid values return validation Problem Details without invoking the device service.
Device-history actions forward request cancellation to their query services.

| Source | Endpoints |
| --- | --- |
| `SystemController` (always) | `GET /api/system` |
| `BuderusController` | Buderus KM200 endpoints |
| `DoorBirdController` | DoorBird endpoints |
| `FroniusController` | Fronius solar inverter endpoints |
| `KnxController` | KNX bus endpoints |

### Health Checks

Health check endpoints:

| Path | Probe type |
| --- | --- |
| `/healthz` | All checks |
| `/healthz/ready` | `Readiness` checks |
| `/healthz/live` | `Liveness` checks |
| `/healthz/startup` | `Startup` checks |

Globally registered checks:

| Check | Default probe type |
| --- | --- |
| Redis | `Liveness` (configurable via `CachingConfig.HealthCheckRedis`) |
| Azure Blob Storage | `Startup` (configurable via `AppConfig.HealthCheckAzureBlobStorage`) |

Each feature library registers its own device connectivity health check when the corresponding flag is active.

### Observability

- **Serilog** structured logging with Grafana Loki sink, console, file, and OpenTelemetry sinks. Enriched with assembly name, span, process, thread, environment, and exception details.
- **OpenTelemetry** metrics (OTLP + Prometheus), traces (OTLP), and logs (OTLP). Instruments ASP.NET Core, gRPC client, HTTP client, Redis, process, and runtime.
- **Swagger UI** at `/{AppConfig.SwaggerUriRoutePrefix}` (default `/swagger`). Requires Basic auth in production.
- **MCP server** at `{AppConfig.McpUrl}` (default `/mcp`). Each enabled feature registers its `[McpServerTool]`-decorated query service methods.

### Agent Runtime

The server does not construct local agents or bind `AIConfig`. Signal and media turns execute through
tenant-scoped definitions in the remote Agent Runtime.

## Configuration

Configuration is bootstrapped via `InitializeConfiguration` (from `CasCap.App`). The provider order is documented in [CasCap.App](../CasCap.App/README.md#configuration-precedence).
Local overrides and user secrets are included before environment variables, with optional Key Vault
last. The following abbreviated order shows the main providers:

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Ignored local overrides and user secrets
4. Environment variables
5. Azure Key Vault (when enabled)

Key configuration sections:

| Section | Type | Description |
| --- | --- | --- |
| `AppConfig` | `AppConfig` | Master application settings (feature flags, Azure, metrics) |
| `ConnectionStrings` | `ConnectionStrings` | Redis, Azure Storage, SignalR Hub |
| `CasCap:HostLifecycleConfig` | `HostLifecycleConfig` | Graceful shutdown timeout; base configuration uses 90 seconds |
| `CasCap:ApiAuthConfig` | `ApiAuthConfig` | Basic auth credentials |
| `CasCap:SignalRHubConfig` | `SignalRHubConfig` | Hub path and sink configuration |
| `CasCap:BuderusConfig` | `BuderusConfig` | Buderus KM200 device settings |
| `CasCap:DoorBirdConfig` | `DoorBirdConfig` | DoorBird device settings |
| `CasCap:FroniusConfig` | `FroniusConfig` | Fronius inverter settings |
| `CasCap:KnxConfig` | `KnxConfig` | KNX bus settings |
| `CasCap:SicceConfig` | `SicceConfig` | Sicce pump settings |
| `CasCap:SignalizrClientConfig` | `SignalizrClientConfig` | Signalizr gateway addresses and durable subscriber name |
| `CasCap:CommsConfig` | `CommsConfig` | Shared Signalizr communications pipeline settings; see [CasCap.Comms](https://github.com/f2calv/signalizr/tree/main/src/CasCap.Comms) |
| `CasCap:MediaConfig` | `MediaConfig` | Media routing, Redis image cache, and stream processing settings |
| `CasCap:BuderusCommsConfig` | `BuderusCommsConfig` | Buderus DHW alert hysteresis and cooldown settings |
| `CasCap:DDnsConfig` | `DDnsConfig` | Dynamic DNS settings |

## Configuration Examples

Merge this feature-selection fragment into the tracked base configuration; it is not a complete
configuration file:

```json
{
  "AppConfig": { "KeyVaultName": "skip" },
  "CasCap": {
    "FeatureConfig": { "EnabledFeatures": "Ubiquiti" }
  }
}
```

For a multi-feature instance, select only configured integrations:

```json
{
  "AppConfig": { "KeyVaultName": "skip" },
  "CasCap": {
    "FeatureConfig": { "EnabledFeatures": "Fronius,Ubiquiti,SignalRHub" },
    "SignalRHubConfig": { "SignalRHub": "http://localhost:8080" }
  }
}
```

Supply the corresponding [Fronius](../CasCap.Api.Fronius/README.md#configuration-examples) and
[Ubiquiti](../CasCap.Api.Ubiquiti/README.md#configuration-examples) options, Redis and API
authentication settings through the base/private configuration providers. Device features can
perform real reads and writes; feature selection is not a simulation mode.

## Dependencies

### NuGet packages

| Package | Purpose |
| --- | --- |
| [Asp.Versioning.Mvc.ApiExplorer](https://www.nuget.org/packages/asp.versioning.mvc.apiexplorer) | API versioning |
| [AspNetCore.HealthChecks.Azure.Data.Tables](https://www.nuget.org/packages/aspnetcore.healthchecks.azure.data.tables) | Azure Tables health check |
| [AspNetCore.HealthChecks.Azure.Storage.Blobs](https://www.nuget.org/packages/aspnetcore.healthchecks.azure.storage.blobs) | Azure Blob health check |
| [AspNetCore.HealthChecks.UI](https://www.nuget.org/packages/aspnetcore.healthchecks.ui).* | Health check dashboard UI |
| [Microsoft.AspNetCore.SignalR.StackExchangeRedis](https://www.nuget.org/packages/microsoft.aspnetcore.signalr.stackexchangeredis) | SignalR Redis backplane |
| [Swashbuckle.AspNetCore](https://www.nuget.org/packages/swashbuckle.aspnetcore) | Swagger/OpenAPI documentation |
| [Azure.Extensions.AspNetCore.Configuration.Secrets](https://www.nuget.org/packages/azure.extensions.aspnetcore.configuration.secrets) | Azure Key Vault configuration |

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.App` | Shared configuration bootstrap (`InitializeConfiguration`) |
| `CasCap.Common.Hosting.AspNetCore` | Serilog hosting and OpenTelemetry metrics, traces, and log exporters |

## License

This project is released under the [MIT License](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
