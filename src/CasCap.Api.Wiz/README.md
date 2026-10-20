# CasCap.Api.Wiz

A .NET library that integrates with [WiZ](https://www.wizconnected.com) smart bulbs on the local network via the Wiz UDP/JSON protocol, providing bulb discovery, state querying and light control.

## Purpose

This source-only library targets .NET 10 and is not published as a NuGet package. Discovery and
commands need UDP access to the bulbs' LAN; routed or container networks may need an explicit
`BindAddress` and subnet broadcast address.

## Usage

```csharp
using CasCap.Extensions;
using CasCap.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWiz(builder.Configuration);
using var host = builder.Build();
var bulbs = await host.Services.GetRequiredService<WizClientService>().DiscoverBulbsAsync();
```

Load the configuration below before registration. In SmartHaus enable `Wiz` in
`CasCap:FeatureConfig:EnabledFeatures`; its feature runner performs periodic discovery.
`SetPilotAsync` changes a bulb's state.

## Discovery

The library is built around one background service and a client/query service pair:

**`WizDiscoveryBgService`** broadcasts a UDP registration message at `DiscoveryPollingDelayMs`
intervals, tracks pilot state and emits `WizEvent` to registered sinks on first sight or a state
change. MAC/RSSI-only changes do not trigger an event.

`AddWiz` does not scan the core assembly for Console/Memory event sinks. Their configuration names
alone do not activate them; register a sink explicitly in a custom host, or use
[AddWizWithExtraSinks](../CasCap.Api.Wiz.Sinks/README.md) for Redis/Azure Tables.

**`WizClientService`** provides the low-level UDP communication with bulbs:

| Method | Description |
| --- | --- |
| `DiscoverBulbsAsync()` | Broadcasts a registration message and collects responding bulbs |
| `GetPilotAsync(bulbIp)` | Retrieves the current state (on/off, brightness, colour, scene) |
| `GetSystemConfigAsync(bulbIp)` | Retrieves firmware version, MAC address and module info |
| `SetPilotAsync(bulbIp, request)` | Sets the bulb state — on/off, brightness, colour, temperature or scene |

**`WizQueryService`** is a facade implementing [`IWizQueryService`](https://github.com/f2calv/SmartHaus/blob/main/src/CasCap.Api.Wiz/Abstractions/IWizQueryService.cs) that delegates to the client service with logging.

## Configuration

Registered via `IServiceCollection.AddWiz()`. Configuration section: `CasCap:WizConfig`.

| Setting | Type | Default | Required | Description |
| --- | --- | --- | --- | --- |
| `BulbPort` | `int` | `38899` | ✓ | UDP port for sending commands to bulbs |
| `DiscoveryTimeoutMs` | `int` | `3000` | ✓ | Timeout for discovery broadcasts |
| `CommandTimeoutMs` | `int` | `2000` | ✓ | Timeout for individual bulb commands |
| `DiscoveryPollingDelayMs` | `int` | `10000` | ✓ | Delay between background discovery cycles |
| `BindAddress` | `string?` | `null` | — | Optional local interface address for discovery and commands |
| `Devices` | `WizDevice[]` | `[]` | — | Known bulbs with friendly names |
| `Sinks` | `SinkConfig` | — | ✓ | Sink selection, also used by the optional storage extension |
| `BroadcastAddress` | `string` | `"255.255.255.255"` | ✓ | Broadcast address for bulb discovery |
| `HealthCheckUri` | `string` | `"255.255.255.255"` | ✓ | Required by `IHealthCheckConfig`; not used by `WizConnectionHealthCheck` (which checks discovered bulb count) |
| `HealthCheck` | `KubernetesProbeTypes` | `Readiness` | ✓ | Kubernetes probe type for the health check tag |

## Configuration Examples

### Minimal

```json
{
  "CasCap": {
    "WizConfig": {
      "Sinks": { "AvailableSinks": { "Console": { "Enabled": true } } }
    }
  }
}
```

### Fully configured

```json
{
  "CasCap": {
    "WizConfig": {
      "BulbPort": 38899,
      "DiscoveryTimeoutMs": 3000,
      "CommandTimeoutMs": 2000,
      "DiscoveryPollingDelayMs": 10000,
      "BroadcastAddress": "255.255.255.255",
      "HealthCheckUri": "255.255.255.255",
      "HealthCheck": "Readiness",
      "Sinks": { "AvailableSinks": { "Console": { "Enabled": true }, "Memory": { "Enabled": true } } }
    }
  }
}
```

## Health Check

**`WizConnectionHealthCheck`** – Verifies that at least one Wiz bulb has been discovered on the local network. Returns `Healthy` when bulbs are found and `Degraded` when none are discovered.

## Event Flow

```mermaid
flowchart TD
    NETWORK["Local Network\n(UDP broadcast)"]

    subgraph BgService["WizDiscoveryBgService (every 10 s by default)"]
        DISCOVER["DiscoverBulbsAsync()"]
        LOG["Log discovered bulbs"]
        COMPARE["Compare pilot state"]
        EMIT["Emit changed WizEvent to sinks"]
    end

    subgraph Client["WizClientService"]
        DISC["DiscoverBulbsAsync()"]
        GET_PILOT["GetPilotAsync(bulbIp)"]
        GET_SYS["GetSystemConfigAsync(bulbIp)"]
        SET_PILOT["SetPilotAsync(bulbIp, request)"]
    end

    NETWORK -->|UDP JSON| Client
    DISCOVER --> DISC
    DISC --> LOG
    LOG --> COMPARE --> EMIT
```

## Dependencies

### NuGet packages

| Package | Purpose |
| --- | --- |
| [OpenWiz](https://www.nuget.org/packages/OpenWiz) | .NET library for communicating with Wiz smart lights |
| [Asp.Versioning.Mvc](https://www.nuget.org/packages/asp.versioning.mvc) | API versioning |
| [Microsoft.AspNetCore.Mvc.Core](https://www.nuget.org/packages/microsoft.aspnetcore.mvc.core) | MVC core for REST controllers |
| [Microsoft.Extensions.Http](https://www.nuget.org/packages/microsoft.extensions.http) | `HttpClient` factory |
| [Microsoft.Extensions.Diagnostics.HealthChecks](https://www.nuget.org/packages/microsoft.extensions.diagnostics.healthchecks) | Health check abstractions |
| [CasCap.Common.Extensions](https://www.nuget.org/packages/cascap.common.extensions) | Shared extension helpers |
| [CasCap.Common.Logging](https://www.nuget.org/packages/cascap.common.logging) | Structured logging helpers |
| [CasCap.Common.Serialization.Json](https://www.nuget.org/packages/cascap.common.serialization.json) | JSON serialisation helpers |
| [CasCap.Common.Extensions.Diagnostics.HealthChecks](https://www.nuget.org/packages/cascap.common.extensions.diagnostics.healthchecks) | Kubernetes probe tag helpers |
| [CasCap.Common.Configuration](https://www.nuget.org/packages/cascap.common.configuration) | Configuration binding helpers |

## License

This project is released under the [MIT License](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
