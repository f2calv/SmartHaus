---
description: Create a new Feature library (+ Sinks library) for the KNX smart home application following all established conventions.
mode: agent
tools:
  - read_file
  - replace_string_in_file
  - multi_replace_string_in_file
  - create_file
  - file_search
  - grep_search
  - semantic_search
  - list_dir
  - run_in_terminal
  - get_errors
  - vscode_askQuestions
---

# New Feature Library

Create a new Feature integration library pair for the KNX smart home application.

## User Input Required

Ask the user for the following before proceeding:

1. **Feature name** (PascalCase, e.g. `Fronius`, `Shelly`, `Ubiquiti`) — this becomes `CasCap.Api.{Feature}` and `CasCap.Api.{Feature}.Sinks`.
2. **MCP domain name** — the human/LLM-friendly domain label (e.g. `Inverter`, `SmartPlug`, `Cameras`) used for the MCP query service class name `{Domain}McpQueryService` and DI method `Add{Domain}Mcp()`.
3. **Event properties** — what telemetry fields the device produces (e.g. temperature, power, state, RSSI). Include types and units.
4. **Device identifier** — which property uniquely identifies a device instance (e.g. `DeviceId`, `Mac`, `CameraId`). This becomes the Azure Table `PartitionKey` for multi-device scenarios.
5. **Single device or multi-device?** — determines whether Azure Table `PartitionKey` is date-based (`yyMMdd`) or entity-scoped (DeviceId).
6. **Data source type** — HTTP polling, UDP discovery, event stream, GPIO, etc. This determines the background service pattern.
7. **CommsStream alert conditions** — what thresholds or state changes should trigger comms alerts (e.g. "temperature above X", "state changed from on to off").

## Checklist — Files to Create/Modify

Work through each section below in order. Mark each step complete before moving to the next.

---

### Phase 1: Base Feature Library — `CasCap.Api.{Feature}/`

#### 1.1 Create project file

Create `src/CasCap.Api.{Feature}/CasCap.Api.{Feature}.csproj`:

- `<TargetFramework>net10.0</TargetFramework>`
- `<InternalsVisibleTo Include="CasCap.Api.{Feature}.Sinks" />`
- Reference pattern: Debug uses `<ProjectReference>`, Release uses `<PackageReference>` — follow existing `.csproj` files in the workspace for the exact conditional include pattern.
- Common package references: `Asp.Versioning.Mvc`, `Microsoft.AspNetCore.Mvc.Core`, `Microsoft.Extensions.Http`, `Microsoft.Extensions.Diagnostics.HealthChecks`.
- Common project references: `CasCap.Common.Configuration`, `CasCap.Common.Extensions`, `CasCap.Common.Logging`, `CasCap.Common.Net`, `CasCap.Common.Serialization.Json`.

#### 1.2 Create `GlobalUsings.cs`

File: `src/CasCap.Api.{Feature}/GlobalUsings.cs`

Follow pure alphabetical ordering. Typical global usings:

```csharp
global using CasCap.Abstractions;
global using CasCap.Models;
global using CasCap.Services;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using System.ComponentModel;
```

#### 1.3 Create `Models/{Feature}Event.cs`

- `public record {Feature}Event` with all telemetry properties.
- Must include `DeviceId` as a `required string` property (first property).
- Must include `DateTime TimestampUtc { get; init; }`.
- Add `[Description("...")]` on every property (MCP-consumable).
- Add XML `<summary>` on every property.
- If the event is constructed from an API response, add a constructor that accepts the response DTO and maps fields.
- Add an `internal` constructor for test factories.

#### 1.4 Create `Models/_{Feature}Config.cs`

- `public record {Feature}Config : IAppConfig` with:
  - `string? AzureTableStorageConnectionString { get; init; }` (if using AzTables)
  - `string? BaseAddress { get; init; }` (if HTTP polling)
  - `int PollingDelayMs { get; init; } = 60_000;` (with `[Range(1, int.MaxValue)]`)
  - `int RedisSeriesExpiryDays { get; init; } = 7;` (with `[Range(1, 365)]`)
  - `SinkConfig Sinks { get; init; } = new();` (with `[ValidateObjectMembers]`)
  - Feature-specific thresholds for CommsStream alerts (e.g. `double AlertThresholdC`, `double AlertHysteresis`, `int AlertCooldownMs`).
- Add `[Url]` on URI properties, `[Range]` on numeric properties, `[MinLength(1)]` on identifier strings.
- Config section name: `CasCap:{Feature}Config`.

#### 1.5 Create `Models/_Enums.cs` (if needed)

- Consolidate all feature-specific enums into a single `_Enums.cs` file.
- Add XML `<summary>` on every enum and member.

#### 1.6 Create `Models/Dtos/` (if needed)

- API response DTOs for the device protocol.
- One class per file matching the type name.

#### 1.7 Create `Abstractions/I{Feature}Query.cs`

```csharp
namespace CasCap.Abstractions;

public interface I{Feature}Query
{
    Task<List<{Feature}Snapshot>> GetSnapshots();
    IAsyncEnumerable<{Feature}Event> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default);
}
```

#### 1.8 Create `Abstractions/I{Feature}QueryService.cs`

```csharp
namespace CasCap.Abstractions;

public interface I{Feature}QueryService
{
    Task<List<{Feature}Snapshot>> GetSnapshots();
    IAsyncEnumerable<{Feature}Event> GetEvents(string? id = null, int limit = 1000, CancellationToken cancellationToken = default);
}
```

#### 1.9 Create `Models/{Feature}Snapshot.cs` (if multi-device)

- `public record {Feature}Snapshot` with summary fields + `DateTimeOffset? ReadingUtc`.
- Add `[Description("...")]` on every property.

#### 1.10 Create `Services/{Feature}ClientService.cs` (if HTTP-based)

- Inject `ILogger`, `IOptions<{Feature}Config>`, `IHttpClientFactory`.
- Use primary constructor.
- Named `HttpClient` matching the health check class name.

#### 1.11 Create `Services/{Feature}MonitorBgService.cs`

- Implement `IBgFeature` (or `BackgroundService` if non-standard polling).
- `FeatureName` property returns `nameof({Feature})` matching the `FeatureNames` constant.
- Poll device, construct `{Feature}Event`, fan out to all `IEventSink<{Feature}Event>` instances.
- Log with `{ClassName}` first pattern.

#### 1.12 Create `Services/{Feature}QueryService.cs`

- Implement `I{Feature}QueryService`.
- Inject `ILogger`, `IEnumerable<IEventSink<{Feature}Event>>`, resolve `I{Feature}Query` from sinks.
- Delegate `GetSnapshots()` and `GetEvents()` to the primary query-capable sink.

#### 1.13 Create `Services/Sinks/{Feature}SinkConsoleService.cs`

```csharp
[SinkType("Console")]
public class {Feature}SinkConsoleService(ILogger<{Feature}SinkConsoleService> logger) : IEventSink<{Feature}Event>
```

- Log the event at `LogDebug` level.

#### 1.14 Create `Services/Sinks/{Feature}SinkMemoryService.cs`

```csharp
[SinkType("Memory")]
public class {Feature}SinkMemoryService(ILogger<{Feature}SinkMemoryService> logger) : IEventSink<{Feature}Event>, I{Feature}Query
```

- `ConcurrentDictionary<string, {Feature}Event>` keyed by `DeviceId`.
- Implements `I{Feature}Query` for snapshot + event retrieval.

#### 1.15 Create `Services/Sinks/{Feature}SinkMetricsService.cs`

```csharp
[SinkType("Metrics")]
public class {Feature}SinkMetricsService : IEventSink<{Feature}Event>
```

- Use `IMeterFactory` to create OTel gauges for key numeric properties.

#### 1.16 Create `Controllers/{Feature}Controller.cs`

- `[ApiController]`, `[Route("api/v{version:apiVersion}/{feature}")]`, `[ApiVersion("1.0")]`.
- Inject `I{Feature}QueryService`.
- `GET /snapshots` → `GetSnapshots()`
- `GET /events` → `GetEvents()`

#### 1.17 Create `HealthChecks/{Feature}ConnectionHealthCheck.cs`

- Implement `IHealthCheck`.
- Use named `HttpClient` to verify device connectivity.

#### 1.18 Create `Extensions/ServiceCollectionExtensions.cs`

- `public static void Add{Feature}(this IServiceCollection services, IConfiguration configuration, bool lite = false, Action<{Feature}Config>? configure = null)`
- Pattern:
  1. Bind config via `AddAndGetCasCapConfiguration<{Feature}Config>()`.
  2. Register named `HttpClient`.
  3. Register client service.
  4. Auto-discover sinks via `AddEventSinks<{Feature}Event>(config.Sinks, typeof(...).Assembly)`.
  5. Ensure primary keyed sink (fallback to Memory).
  6. Unless `lite`: register health check + `IBgFeature` background service.
  7. Register `{Feature}QueryService` + `I{Feature}QueryService`.

#### 1.19 Create `README.md`

- Purpose → Services/Extensions → Configuration → Dependencies (NuGet packages table + Project references table).
- Include `## Configuration Examples` section with `appsettings.json` snippets.

---

### Phase 2: Sinks Library — `CasCap.Api.{Feature}.Sinks/`

#### 2.1 Create project file

Create `src/CasCap.Api.{Feature}.Sinks/CasCap.Api.{Feature}.Sinks.csproj`:

- `<TargetFramework>net10.0</TargetFramework>`
- Always reference: `<ProjectReference Include="..\CasCap.Api.{Feature}\CasCap.Api.{Feature}.csproj" />`
- Debug references: `CasCap.Api.Azure.Auth`, `CasCap.Api.Azure.Storage`, `CasCap.Common.Caching`.

#### 2.2 Create `GlobalUsings.cs`

```csharp
global using Azure;
global using Azure.Data.Tables;
global using CasCap.Abstractions;
global using CasCap.Models;
global using CasCap.Services;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using System.Runtime.CompilerServices;
global using System.Runtime.Serialization;
```

#### 2.3 Create `Models/{Feature}ReadingEntity.cs`

- `public class {Feature}ReadingEntity : ITableEntity`
- Parameterless constructor (required by SDK).
- Event-accepting constructor: `public {Feature}ReadingEntity({Feature}Event evt)`.
  - `PartitionKey` = `evt.DeviceId` (multi-device) or `evt.TimestampUtc.ToString("yyMMdd")` (single-device).
  - `RowKey` = `evt.TimestampUtc.Ticks.ToString()` (raw Ticks, **never** divided by 10_000).
  - Map event properties to ultra-short column names (single letters where possible).
- Ultra-short `get; init;` properties for storage columns (e.g. `public double p { get; init; }`).
- Full-name expression-bodied accessor properties with `[IgnoreDataMember]` (e.g. `public double Power => p;`).
- `TimestampUtc` accessor: `public DateTime TimestampUtc => new(long.Parse(RowKey), DateTimeKind.Utc);`
- `GetEntity()` method returning a `TableEntity` with all column fields.

#### 2.4 Create `Models/{Feature}SnapshotEntity.cs`

- `public class {Feature}SnapshotEntity : ITableEntity`
- Parameterless constructor.
- Event-accepting constructor:
  - If `RowKey` is derivable from the event (e.g. `DeviceId`): `public {Feature}SnapshotEntity(string partitionKey, {Feature}Event evt)` — derive `RowKey` internally.
  - If `RowKey` is a constant (single-device, e.g. `"latest"`): `public {Feature}SnapshotEntity(string partitionKey, string rowKey, {Feature}Event evt)`.
- Use readable column names (snapshot is low-volume).
- Include `DateTimeOffset? ReadingUtc { get; init; }`.
- `GetEntity()` method.

#### 2.5 Create `Services/Sinks/{Feature}SinkAzTablesService.cs`

```csharp
[SinkType("AzureTables")]
public class {Feature}SinkAzTablesService : IEventSink<{Feature}Event>, I{Feature}Query
```

- Inject `ILogger`, `IOptions<AzureAuthConfig>`, `IOptions<{Feature}Config>`.
- Read table names from `config.Sinks.AvailableSinks["AzureTables"].Settings` using `SinkSettingKeys.LineItemTableName` / `SinkSettingKeys.SnapshotTableName`.
- Fallback table names: `"{feature}lineitemsv1"`, `"{feature}snapshotv1"`.
- `WriteEvent()`: create both line item + snapshot entities, upsert both in parallel.
- `GetSnapshots()`: query snapshot table by `SnapshotPartitionKey`.
- `GetEvents()`: query line items, optionally filtered by `id` (= PartitionKey = DeviceId).

#### 2.6 Create `Services/Sinks/{Feature}SinkRedisService.cs`

```csharp
[SinkType("Redis")]
public class {Feature}SinkRedisService : IEventSink<{Feature}Event>, I{Feature}Query
```

- Inject `ILogger`, `IOptions<{Feature}Config>`, `IRemoteCacheService`.
- Read Redis key names from sink settings: `SnapshotValues` and `SeriesValues`.
- `WriteEvent()`:
  1. Upsert snapshot hash: key = `{snapshotKey}:{DeviceId}`, fields = property name/value pairs.
  2. Add to series sorted set: key = `{seriesKey}:{yyMMdd}`, score = `Ticks`, member = pipe-delimited values.
  3. Set series TTL from `config.RedisSeriesExpiryDays`.
- `GetSnapshots()`: scan snapshot keys, `HGETALL` each.
- `GetEvents()`: `ZRANGEBYSCORE` on today's series key.

#### 2.7 Create `Services/Sinks/{Feature}SinkCommsStreamService.cs`

```csharp
[SinkType("CommsStream")]
public class {Feature}SinkCommsStreamService : IEventSink<{Feature}Event>
```

- Inject `ILogger`, `IOptions<{Feature}Config>`, `IEventSink<CommsEvent>`.
- Implement hysteresis + cooldown pattern for threshold alerts:
  - Read threshold, hysteresis, and cooldown from config.
  - Track per-device state in `ConcurrentDictionary<string, (bool IsAlerting, DateTime LastAlertUtc)>`.
  - Alert when value exceeds threshold (and not in cooldown).
  - Rearm when value drops below `threshold - hysteresis`.
- For state-change alerts (on/off): track previous state per device, alert on transitions (skip first-seen).

#### 2.8 Create `Extensions/ServiceCollectionExtensions.cs`

```csharp
public static class {Feature}SinksServiceCollectionExtensions
{
    public static void Add{Feature}WithExtraSinks(this IServiceCollection services, IConfiguration configuration,
        bool lite = false, Action<{Feature}Config>? configure = null)
    {
        services.Add{Feature}(configuration, lite, configure);
        var config = configuration.GetCasCapConfiguration<{Feature}Config>();
        services.AddEventSinks<{Feature}Event>(
            lite ? config.Sinks.WithoutSinkType("Redis") : config.Sinks,
            typeof({Feature}SinksServiceCollectionExtensions).Assembly);
    }
}
```

#### 2.9 Create `README.md`

- Purpose → Sinks overview → Configuration → Dependencies.

---

### Phase 3: MCP Integration — `CasCap.Common.AI/`

#### 3.1 Create `Services/Mcp/{Domain}McpQueryService.cs`

```csharp
[McpServerToolType]
public class {Domain}McpQueryService(I{Feature}QueryService querySvc)
```

- `[McpServerTool]` + `[Description("...")]` on every method.
- `[Description("...")]` on every non-CancellationToken parameter.
- Methods: `Get{Feature}Snapshot`, `Get{Feature}Events`.
- Domain-prefix tool names for global uniqueness.

#### 3.2 Update `Extensions/ServiceCollectionExtensions.cs`

Add `Add{Domain}Mcp()` method:

```csharp
public static void Add{Domain}Mcp(this IServiceCollection services)
    => services.AddSingleton<{Domain}McpQueryService>();
```

---

### Phase 4: Wiring — Composition Root

#### 4.1 Update `FeatureNames.cs`

File: `src/CasCap.SmartHaus/Models/FeatureNames.cs`

Add:

```csharp
/// <summary>{Feature description} integration.</summary>
public const string {Feature} = nameof({Feature});
```

#### 4.2 Update `Program.cs`

File: `src/CasCap.App.Server/Program.cs`

Add feature registration block (follow existing pattern):

```csharp
if (enabledFeatures.Contains(FeatureNames.{Feature}) || enabledFeatures.Contains(FeatureNames.Comms))
{
    builder.Services.Add{Feature}WithExtraSinks(builder.Configuration,
        lite: !enabledFeatures.Contains(FeatureNames.{Feature}));
    builder.Services.Add{Domain}Mcp();
    mcpBuilder.WithToolsFromAssembly(typeof({Domain}McpQueryService).Assembly);
    mcpBuilder.WithPromptsFromAssembly(typeof({Domain}McpQueryService).Assembly);
}
```

Also add the controller application part:

```csharp
if (enabledFeatures.Contains(FeatureNames.{Feature}))
    mvcBuilder.AddApplicationPart(typeof({Feature}Controller).Assembly);
```

---

### Phase 5: Configuration Files

#### 5.1 Update `appsettings.json`

Add `{Feature}Config` section under `CasCap:`:

```json
"{Feature}Config": {
  "AzureTableStorageConnectionString": "https://yourstore.table.core.windows.net/",
  "BaseAddress": "http://192.168.1.xxx",
  "PollingDelayMs": 60000,
  "RedisSeriesExpiryDays": 7,
  "Sinks": {
    "AvailableSinks": {
      "AzureTables": {
        "Enabled": true,
        "Settings": {
          "LineItemTableName": "{feature}lineitemsv1",
          "SnapshotTableName": "{feature}snapshotv1"
        }
      },
      "Memory": { "Enabled": false },
      "Metrics": { "Enabled": true },
      "Console": { "Enabled": true },
      "Redis": {
        "Enabled": true,
        "Settings": {
          "SnapshotValues": "{feature}:snapshot:values",
          "SeriesValues": "{feature}:series"
        }
      },
      "CommsStream": { "Enabled": true }
    }
  }
}
```

#### 5.2 Update `appsettings.Development.json`

Add development-specific overrides (e.g. shorter polling, disabled sinks).

#### 5.3 Update `EnabledFeatures`

Add `"{Feature}"` to the `EnabledFeatures` array in both `appsettings.json` and `appsettings.Development.json`.

---

### Phase 6: Solution & Helm

#### 6.1 Update `SmartHaus.Debug.slnx`

Add both projects under `/SmartHaus/libs/`:

```xml
<Project Path="src/CasCap.Api.{Feature}/CasCap.Api.{Feature}.csproj" />
<Project Path="src/CasCap.Api.{Feature}.Sinks/CasCap.Api.{Feature}.Sinks.csproj" />
```

#### 6.2 Update `SmartHaus.Release.slnx`

Same additions.

#### 6.3 Update Helm values (if applicable)

File: `charts/smarthaus/values.yaml` — add feature to enabled features list if deploying to K8s.

Also check the KNX_K8S repo: `src/workloads/smarthaus.yaml` — ArgoCD Application manifest may need the feature enabled.

---

### Phase 7: Verification

#### 7.1 Build

Run `dotnet build SmartHaus.Debug.slnx` and fix all errors.

**Do NOT run tests automatically** — prompt the user first (they may be integration tests requiring device connectivity).

#### 7.2 Review checklist

Before declaring complete, verify:

- [ ] All files follow class-per-file convention (filename matches type name).
- [ ] All public types have XML documentation.
- [ ] All `[McpServerTool]` methods have `[Description]` attributes.
- [ ] Config properties have validation attributes.
- [ ] ReadingEntity uses ultra-short column names + `[IgnoreDataMember]` expanded accessors.
- [ ] SnapshotEntity uses readable column names.
- [ ] ReadingEntity constructor accepts event object (not individual properties).
- [ ] SnapshotEntity constructor derives RowKey from event when possible.
- [ ] PartitionKey is entity-scoped for multi-device, date-based for single-device.
- [ ] RowKey uses raw `Ticks.ToString()`.
- [ ] Redis keys follow `{domain}:{type}:{detail}` pattern.
- [ ] Table names follow `{feature}lineitemsv1` / `{feature}snapshotv1` pattern.
- [ ] Logging uses `{ClassName}` first pattern with `nameof()`.
- [ ] Primary constructors used throughout.
- [ ] `GlobalUsings.cs` in both project roots.
- [ ] `README.md` in both project roots.
- [ ] appsettings.json + appsettings.Development.json updated.
- [ ] Solution files updated.
- [ ] FeatureNames.cs updated.
- [ ] Program.cs updated with feature + MCP + controller wiring.
