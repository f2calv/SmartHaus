# CasCap.App.Server

The main ASP.NET Core executable that composes and hosts the entire home automation system. It wires together all feature libraries (`CasCap.Api.*`) based on the comma-separated `EnabledFeatures` configuration string, exposes a versioned REST API, a SignalR hub, Swagger/OpenAPI documentation, MCP tools, and an AI agent.

## Purpose

`CasCap.App.Server` is the single deployable process that conditionally activates features at startup based on the `FeatureConfig.EnabledFeatures` comma-separated string. This enables the same binary to be deployed as different service profiles (e.g. KNX-only pod, full all-in-one instance).

### Feature Flag Registration

Each feature name activates a distinct set of services at DI registration time. At runtime, the non-generic `FeatureFlagBgService` matches each `IBgFeature.FeatureName` string against the enabled features set:

| Flag | Services registered | MCP tools |
| --- | --- | --- |
| `Buderus` | `BuderusKm200MonitorBgService`, controller, sinks | `HeatPumpMcpQueryService` |
| `DoorBird` | Controller, sinks, callback endpoints | `FrontDoorMcpQueryService` |
| `Fronius` | `FroniusMonitorBgService`, controller, sinks | `InverterMcpQueryService` |
| `Knx` | `KnxMonitorBgService`, controller, sinks | `BusSystemMcpQueryService` |
| `EdgeHardware` | `EdgeHardwareMonitorBgService`, CPU/GPU sinks, GPIO sensors | `EdgeHardwareMcpQueryService` (when GPU detected) |
| `Sicce` | `SicceBgService` | — |
| `DDns` | `DDnsBgService` | — |
| `SignalRHub` | `HausHub` SignalR hub, hub sinks, Redis backplane | — |
| `Comms` | `CommunicationsBgService`, signal-cli client, comms stream sink; features also register in **lite mode** (sinks only, no polling) when this flag is set | — |

Features not present in `EnabledFeatures` are entirely absent — no controllers, background services, or health checks are registered for disabled features.

### Always-On Services

The following services are registered regardless of feature flags:

| Service | Description |
| --- | --- |
| `GitMetadataBgService` | Periodically logs git build metadata from environment variables (registered via `AddFeatureFlagService`) |
| `SystemController` | `GET /api/system` — returns `AppConfig` |

### Authentication

| Environment | Scheme | Behaviour |
| --- | --- | --- |
| Development | Basic auth | Default policy allows all requests (no credentials required) |
| Production | Basic auth | `BasicAuthenticationHandler` validates credentials from `ApiAuthConfig` |

### API Endpoints

All controllers are added dynamically based on `EnabledFeatures`. API versioning defaults to v1.

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

### AI Agents

Named `AgentConfig` entries in `AIConfig.Agents` are each registered as a keyed singleton `AIAgent`. Each agent references a `ProviderConfig` and can gather tools from in-process MCP query services (via `ToolServices`) or from remote MCP server endpoints (via `ToolEndpoints`).

## Configuration

Configuration is bootstrapped via `InitializeConfiguration` (from `CasCap.App`). The full configuration hierarchy is:

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Environment variables
4. Azure Key Vault (when `AppConfig.KeyVaultName` is configured)

Key configuration sections:

| Section | Type | Description |
| --- | --- | --- |
| `AppConfig` | `AppConfig` | Master application settings (feature flags, Azure, metrics) |
| `ConnectionStrings` | `ConnectionStrings` | Redis, Azure Storage, SignalR Hub |
| `AIConfig` | `AIConfig` | AI agent configuration (providers, agents) |
| `CasCap:ApiAuthConfig` | `ApiAuthConfig` | Basic auth credentials |
| `CasCap:SignalRHubConfig` | `SignalRHubConfig` | Hub path and sink configuration |
| `CasCap:BuderusConfig` | `BuderusConfig` | Buderus KM200 device settings |
| `CasCap:DoorBirdConfig` | `DoorBirdConfig` | DoorBird device settings |
| `CasCap:FroniusConfig` | `FroniusConfig` | Fronius inverter settings |
| `CasCap:KnxConfig` | `KnxConfig` | KNX bus settings |
| `CasCap:SicceConfig` | `SicceConfig` | Sicce pump settings |
| `CasCap:SignalCliConfig` | `SignalCliConfig` | Signal messenger settings |
| `CasCap:CommsAgentConfig` | `CommsAgentConfig` | Communications agent orchestration settings |
| `CasCap:SecurityAgentConfig` | `SecurityAgentConfig` | Security/vision agent settings |
| `CasCap:HeatingAgentConfig` | `HeatingAgentConfig` | Heating agent settings (DHW1 alert hysteresis, cooldown) |
| `CasCap:DDnsConfig` | `DDnsConfig` | Dynamic DNS settings |

## Dependencies

### NuGet packages

| Package | Purpose |
| --- | --- |
| [Asp.Versioning.Mvc.ApiExplorer](https://www.nuget.org/packages/asp.versioning.mvc.apiexplorer) | API versioning |
| [AspNetCore.HealthChecks.Azure.Data.Tables](https://www.nuget.org/packages/aspnetcore.healthchecks.azure.data.tables) | Azure Tables health check |
| [AspNetCore.HealthChecks.Azure.Storage.Blobs](https://www.nuget.org/packages/aspnetcore.healthchecks.azure.storage.blobs) | Azure Blob health check |
| [AspNetCore.HealthChecks.UI](https://www.nuget.org/packages/aspnetcore.healthchecks.ui).* | Health check dashboard UI |
| [OpenTelemetry](https://www.nuget.org/packages/opentelemetry).* | Metrics, traces, and log exporters |
| [Microsoft.AspNetCore.SignalR.StackExchangeRedis](https://www.nuget.org/packages/microsoft.aspnetcore.signalr.stackexchangeredis) | SignalR Redis backplane |
| [Swashbuckle.AspNetCore](https://www.nuget.org/packages/swashbuckle.aspnetcore) | Swagger/OpenAPI documentation |
| [Azure.Extensions.AspNetCore.Configuration.Secrets](https://www.nuget.org/packages/azure.extensions.aspnetcore.configuration.secrets) | Azure Key Vault configuration |

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.App` | Shared configuration bootstrap (`InitializeConfiguration`) |
| `CasCap.Common.Logging.Serilog` | Serilog structured logging pipeline |

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
