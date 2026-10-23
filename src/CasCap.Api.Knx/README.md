# CasCap.Api.Knx

A .NET library that connects to a [KNX](https://www.knx.org) bus via KNXnet/IP tunnelling or routing (multicast), monitors all group-address telegrams, and fans them out to a configurable set of sinks for persistence, alerting and streaming.

## Installation

Targets .NET 8 and .NET 10. A reachable KNXnet/IP interface and an ETS group-address XML export
matching the naming contract below are required for live bus access.

```bash
dotnet add package CasCap.Api.Knx
```

## Usage

Register the library in a .NET host that supplies logging, configuration, Redis/RedLock and the
shared feature runner:

```csharp
using CasCap.Extensions;

builder.Services.AddKnx(builder.Configuration);
```

`builder` is your host builder. This registers [`IBgFeature`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Abstractions/Abstractions/IBgFeature.cs) workers rather than starting them.
The SmartHaus host supplies their lifecycle through `AddFeatureFlagService`; enable `Knx` in
`CasCap:FeatureConfig:EnabledFeatures` there. Use the configuration examples below and your own
ETS export. Outbound telegrams can change lights, heating, shutters and physical access.

`AddKnxWithExtraSinks` from the
[storage package](https://github.com/f2calv/SmartHaus/tree/main/src/CasCap.Api.Knx.Sinks)
adds persistent state/history. `lite: true` omits bus workers and can read shared Redis state or
publish commands to an existing bus-owning process; it does not create a bus connection.

## Bus Processing

The library's monitor, processor and sender form a pipeline:

1. **`KnxMonitorBgService`** – Loads ETS metadata on every replica and uses the shared leadership runner in Unified mode. Each ownership callback creates a disposable `KnxMonitorService` through [`IKnxMonitor`](./Abstractions/IKnxMonitor.cs). In **Tunneling** mode it establishes one `KnxBus` connection per configured area/line combination; in **Routing** mode it joins the multicast group advertised by the KNX IP router (and `ShardingMode` is forced to `Unified`). Incoming telegrams pass through service-family-specific deduplication filters before being decoded and published via the [`IKnxTelegramBroker<KnxEvent>`](https://github.com/f2calv/SmartHaus/blob/main/src/CasCap.Api.Knx/Abstractions/IKnxTelegramBroker%7BT%7D.cs).
2. **`KnxProcessorBgService`** – Reads from that broker and fans each `KnxEvent` out to every registered [`IEventSink<KnxEvent>`](https://github.com/f2calv/CasCap.Common/blob/main/src/CasCap.Common.Abstractions/Abstractions/IEventSink%7BT%7D.cs) implementation in parallel, waiting for all sinks to complete before processing the next event.

3. **`KnxSenderBgService`** – Runs inside the monitor's ownership callback, not as an independently launched feature. It waits for connectivity before consuming outbound requests and passes the ownership-linked cancellation token to bus writes. Standbys do not consume outgoing commands.

`KnxAutomationBgService` forwards its process-local command queue on every replica as soon as ETS
metadata is ready, including healthy standbys without a local bus connection. Its optional day/night
schedule instead runs inside the monitor's ownership callback, alongside the sender. The first
day/night update follows connectivity and subsequent updates retain `DayNightPollingDelayMs`.
Scheduled writes are awaited with the ownership token, including the optional bedroom override;
they do not enter the host-lived local queue.

A REST API (`KnxController`) and a gRPC endpoint (`RpcTelegramService`) allow external callers to read
state and send commands.

## Group Address Naming Convention

The library parses KNX group address **names** (as exported from ETS) into strongly-typed DTOs. For parsing to succeed, every group address must follow a **hyphen-separated segment pattern** where each segment maps to a known enum value. During construction every recognised segment is consumed from a list; after a successful parse the list should be empty — any leftover segments indicate an unrecognised token.

### Segment Pattern

```text
[Floor]-Category-[Room[(Location)]]-[Orientation]-[HorizontalPos]-[VerticalPos]-[LightStyle]-Function-[Outdoor|Indoor]-[[Identifier]]
```

Segments in square brackets are optional. The parser is **order-independent** — it matches each segment against frozen dictionaries of known values and removes it from the list, so segments can appear in any order. `Category` is the only mandatory segment (addresses without a recognised category are skipped entirely).

### Segment Reference

| Segment | Values | Notes |
| --- | --- | --- |
| **Floor** | `KG` (basement), `EG` (ground), `OG` (upper), `DG` (top) | German abbreviations from the KNX standard. |
| **Category** | `SYS`, `ENV`, `BI`, `BL`, `HZ`, `PM`, `LI`, `SD` | Determines which function enum is used. See table below. |
| **Room** | `Office`, `StorageRoom`, `Loggia`, `Entrance`, `MasterBedroom`, `MasterBathroom`, `FamilyBathroom`, `ChildRoom`, `ChildRoom1`, `ChildRoom2`, `Bedroom`, `Kitchen`, `LivingRoom`, `GuestWC`, `OpenPlanLiving`, `Study`, `Hallway`, `GuestRoom`, `GuestBathroom`, `BoilerRoom`, `LaundryRoom`, `Garage` | Appending text in parentheses adds a free-text location, e.g. `Entrance(FrontDoor)`. |
| **Orientation** | `North`, `East`, `South`, `West` | Compass direction of the device. |
| **HorizontalPos** | `Left` / `L`, `Middle`, `Right` / `R`, `Corner` | `L` and `R` are accepted as aliases. |
| **VerticalPos** | `Top`, `Middle`, `Bottom` | Vertical qualifier for multi-level fixtures. |
| **LightStyle** | `L` (generic), `DL` (downlighter), `WL` (wall light), `PL` (pendulum), `LED` (LED stripe) | Only recognised for `LI` (lighting) addresses. |
| **Function** | Category-specific — see below | Segments ending in `_FB` mark the address as a feedback value. |
| **Outdoor / Indoor** | `Outdoor`, `Indoor` | Sets the `IsOutside` flag. Defaults to indoor when absent. |
| **Identifier** | Text in `[square brackets]` | e.g. `SYS-[DateTime]` → `Identifier = DateTime`. |

### Category → Function Mapping

| Category | Enum | Functions |
| --- | --- | --- |
| `BL` (shutter/blind) | `ShutterFunction` | `POS`, `POS_FB`, `POSSLATS`, `POSSLATS_FB`, `DIRECTION`, `MOVE`, `SCENE`, `STEP`, `WIND`, `RAIN`, `DIAG`, `TT` |
| `BI` (binary contact) | `ContactFunction` | `STATE` |
| `LI` (lighting) | `LightingFunction` | `SW`, `SW_FB`, `DIM`, `VAL`, `VFB`, `STAIRWAY`, `SEQ1`, `SEQ1_FB`, `BITSCENE1`, `RGB`, `RGB_FB`, `RGB_DIM`, `HSV`, `HSV_FB`, `SCENE`, `LUX`, `LOCK` |
| `SD` (power outlet) | `PowerOutletFunction` | `SD_SW`, `SD_FB` |
| `HZ` (heating/HVAC) | `HvacFunction` | `SETP`, `SETP_UPDATE`, `FB`, `OFFSET`, `WINDOW`, `TEMP`, `OUTPUT`, `DIAG`, `HUMIDITY` |
| `SYS` (system) | `SystemFunction` | *(none — identifier-based)* |
| `ENV` (environment) | `EnvironmentFunction` | *(none — identifier-based)* |
| `PM` (presence/motion) | `PresenceFunction` | `INVERT` |

### Examples

| Group Address Name | Floor | Category | Room | Orientation | LightStyle | Function | Feedback | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `DG-LI-Office-SW` | DG | LI | Office | | | SW | | Simple light switch |
| `EG-LI-Entrance-DL-SW_FB` | EG | LI | Entrance | | DL | SW_FB | yes | Downlighter switch feedback |
| `OG-BL-FamilyBathroom-West-POS` | OG | BL | FamilyBathroom | West | | POS | | Blind position, west-facing |
| `EG-BI-Entrance(FrontDoor)-East-STATE` | EG | BI | Entrance | East | | STATE | | Door contact with location |
| `EG-BI-Entrance(Main DoorLock)-North-STATE` | EG | BI | Entrance | North | | STATE | | Door lock using `DPST-1-2` |
| `DG-HZ-StorageRoom-SETP` | DG | HZ | StorageRoom | | | SETP | | Heating setpoint |
| `DG-BI-STATE` | DG | BI | | | | STATE | | Binary contact, no room |
| `SYS-[DateTime]` | | SYS | | | | | | System identifier |
| `EG-PM-Kitchen-South` | EG | PM | Kitchen | South | | | | Presence sensor, no function |
| `EG-LI-Kitchen-LED-Left-SW` | EG | LI | Kitchen | | LED | SW | | LED strip, left position |
| `OG-BL-MasterBedroom-South-Left-MOVE` | OG | BL | MasterBedroom | South | | MOVE | | Blind move, south-left |

### Grouping

Addresses that share all segments except the function suffix are automatically grouped into `KnxGroupAddressGroup` records. For example, `OG-BL-FamilyBathroom-West-MOVE`, `OG-BL-FamilyBathroom-West-POS` and `OG-BL-FamilyBathroom-West-SCENE` all belong to the group `OG-BL-FamilyBathroom-West`.

BI groups whose parenthesized location ends in `DoorLock` and whose `STATE` function uses
`DPST-1-2` are projected as door locks rather than opening contacts. For these installed sensors,
Boolean false means locked and Boolean true means unlocked.

### ETS Setup Requirements

1. **Export format** — Export group addresses from ETS as XML (the library deserialises `KnxGroupAddressXmlExport`).
2. **DPT assignment** — Every group address must have a KNX Datapoint Type assigned in ETS (format `DPST-x-y`). Addresses without a DPT are skipped during parsing.
3. **Unique names** — Each group address name must be unique across the entire project. Duplicate names cause a startup exception.
4. **Placeholder filtering** — Addresses whose name contains a `?` character are treated as ETS placeholders and excluded from the loaded lookup.
5. **Validation** — Run `GroupAddressTests.ParseGroupAddressNamingConvention()` after modifying ETS names. The test asserts that every address is fully parsed (zero leftover segments).

## Configuration

Configure site-specific values under `CasCap:KnxConfig`. The defaults are not a complete KNX
installation: supply an ETS export, interface settings and any enabled automation settings.

### Site-Specific Settings

| Setting | Description | Example |
| --- | --- | --- |
| `TunnelingAreaLineFilter` | Area/line combinations to monitor (tunneling only) | `["1.1", "1.2", "1.3"]` |
| `StateChangeAlerts` | Group address names mapped to LLM-friendly descriptions for state change alerts | `{"SYS-[RM_Alarm_FB]": "Smoke detector alarm", ...}` |
| `DayNightTimeZoneLocation` | IANA time zone for sunrise/sunset | `"Berlin"` |
| `DayNightLatitude` | Latitude for day/night calculation | `52.520008` |
| `DayNightLongitude` | Longitude for day/night calculation | `13.404954` |

### Translation / LLM Glossary

The `Translations` section in `KnxConfig` maps English enum values to localised equivalents. At agent startup the `TranslationExtensions.BuildTranslationGlossary()` method generates a Markdown glossary table that can be injected into the LLM system prompt, allowing end users to query in German (or any configured language) while MCP tools use English internally.

```json
{
  "Translations": {
  "de": {
    "Floors": { "DG": "Dachgeschoss", "OG": "Obergeschoss" },
    "Rooms": { "Kitchen": "Küche", "Office": "Büro" },
    "Orientations": { "North": "Nord", "East": "Ost" },
    "Categories": { "LI": "Beleuchtung", "HZ": "Heizung" }
  },
  "es": {
    "Floors": { "DG": "Ático", "OG": "Planta alta", "EG": "Planta baja", "KG": "Sótano" },
    "Rooms": { "Kitchen": "Cocina", "Office": "Oficina", "LivingRoom": "Salón" },
    "Orientations": { "North": "Norte", "East": "Este", "South": "Sur", "West": "Oeste" },
    "Categories": { "LI": "Iluminación", "HZ": "Calefacción", "BL": "Persianas", "SD": "Enchufe" }
  }
}
}
```

### Sinks

| Sink | Description |
| --- | --- |
| **Console** | Logs every telegram via Serilog (configurable verbosity filter) |
| **Metrics** | Emits OpenTelemetry metrics |

Optional Redis and Azure Tables implementations are provided by
[`CasCap.Api.Knx.Sinks`](https://github.com/f2calv/SmartHaus/tree/main/src/CasCap.Api.Knx.Sinks).

### Deployment Matrix

The three key configuration properties — `ServiceFamily`, `ShardingMode` and `TelegramBrokerMode` — are interdependent. The table below shows every valid combination, what it means for Kubernetes resources, and whether external callers (MCP tools / Agents) can send commands to the KNX bus.

| `ServiceFamily` | `ShardingMode` | `TelegramBrokerMode` | K8s resource | External access | Notes |
| --- | --- | --- | --- | --- | --- |
| Tunneling | Unified | **Redis** (default) | Deployment (1+ replicas) | ✅ Any pod or external service publishes to the Redis outgoing stream; the leader's `KnxSenderBgService` consumes it. | Recommended production configuration. Redlock elects the active pod; standbys are warm. |
| Tunneling | Unified | Channel | — (local dev) | ❌ `Channel<T>` is in-process only — callers in other pods cannot reach it. | Local development only. Override via `CasCap__KnxConfig__TelegramBrokerMode=Channel`. |
| Tunneling | Partitioned | **Redis** (required) | StatefulSet | ✅ Same Redis stream mechanism. Each pod owns one KNX line. | ⚠️ Not yet implemented. Redis is mandatory — each pod must share telegrams across the cluster. |
| Tunneling | Partitioned | Channel | — | ❌ Invalid | Partitioned mode requires cross-pod communication; Channel cannot provide it. |
| Routing | Unified (forced) | **Redis** (default) | Deployment (1+ replicas) | ✅ Same as Tunneling + Unified + Redis. | `ShardingMode` is forced to Unified at startup (with a warning if configured otherwise). |
| Routing | Unified (forced) | Channel | — (local dev) | ❌ Same limitation as above. | Local development only. |
| Routing | Partitioned | — | — | ❌ Invalid | Routing receives all backbone traffic on one multicast connection — partitioning is meaningless. Forced to Unified at startup. |

> **Why is Redis the default?** The .NET application has no knowledge of its replica count — that is a Kubernetes concern. By defaulting to `Redis`, the application works correctly in any deployment topology (single-replica, multi-replica, external Agents). The Helm chart or `appsettings.json` can override to `Channel` for local development where Redis is not available.

### Sharding Modes

The `ShardingMode` property on `KnxConfig` controls how `KnxMonitorBgService` pods are distributed across KNX lines. The mode is set via `appsettings.json` under `CasCap:KnxConfig:ShardingMode`.

| Mode | Kubernetes Resource | Description |
| --- | --- | --- |
| **Unified** (default) | `Deployment` (1+ replicas) | A single active pod processes telegrams from **all** configured lines. Additional replicas remain on standby via Redlock election. Takeover depends on lease expiry, contention timing and connection startup, not immediate failover. **Forced** when `ServiceFamily` is `Routing`. |
| **Partitioned** | `StatefulSet` | Each pod connects to exactly **one** KNX line, determined by mapping the pod ordinal to an entry in `TunnelingAreaLineFilter` (e.g. `haus-knx-0` → `1.1`, `haus-knx-1` → `1.2`). The replica count must equal the number of configured lines. Only valid when `ServiceFamily` is `Tunneling`. ⚠️ *Not yet implemented — selecting this mode throws `NotSupportedException`.* |

#### Unified Mode Startup Sequence

1. **Group address lookup** — all replicas load the ETS XML metadata concurrently (stateless, no lock required).
2. **Configuration logged** — after the lookup completes, `ServiceFamily` and `ShardingMode` are emitted at `Information` level so the active configuration is visible in every pod's log stream. If `ServiceFamily` is `Routing` and `ShardingMode` was set to `Partitioned`, it is overridden to `Unified` with a warning.
3. **Leadership election** — the shared runner acquires the unchanged resource `knx:monitor`, using the existing caching factory/database/key format and `CachingConfig.Redlock` configuration. The built-in `LeaderElection` profile defaults to 30 s expiry, 60 s acquisition wait and 5 s retry; application overrides still apply. Root cache-miss timings are unchanged.
4. **Leader runs** — each ownership run gets fresh connection, callback-buffer and reconnect state. Health monitoring, reconnection, incoming publication, outbound delivery and enabled day/night scheduling are supervised together. A fatal child failure withdraws readiness and requests host shutdown immediately, before cancelling/joining siblings and disposing connections. Ownership-only cancellation does not stop the host. Initial connection failures and subsequent drops are retried for both tunneling and routing.
5. **Ownership ends** — the runner observes lease loss at `Redlock.LeadershipCheckIntervalMs` (default 1000 ms) and cancels the ownership token. Health is withdrawn, callbacks stop accepting telegrams, and all publication/write work is awaited before handlers are detached and buses asynchronously disposed. Cleanup is also applied to partial startup failures. Only then may this replica start a fresh ownership run. Host shutdown owns the forced-termination budget; cleanup has no independent timeout.

This checkout uses the provisional .NET 8+ runner from sibling Debug project references. Release
packages do not yet contain that API; validate with `SmartHaus.Debug.slnx` until the shared package
release is available.

Lease cancellation cannot retract a telegram already accepted by Redis or a device. Callback
buffers are in-memory and are not replayed across ownership runs. Outgoing acknowledgement and
retry behavior remains broker-specific; this lifecycle does not provide exactly-once delivery or
downstream fencing. Offline tests cover supervision and cleanup, not live lease renewal, partitions
or physical bus behavior.

Local command processing retains bounded concurrency and awaits in-flight work during shutdown.
Dequeue remains destructive: cancellation or process failure after dequeue can lose that item;
no automatic requeue or stronger delivery guarantee is added. Falcon disposal is awaited without an
application timeout or cancellation token. Its internal bound has not been verified; immediate
host-stop signalling starts the host's termination budget even if sibling or SDK cleanup stalls.

The sender can use surviving connections while another line reconnects; the overall connection
health check still requires every configured connection. Redis operations already dispatched are
awaited even when cancellation arrives. Stream acknowledgement follows processing, but pending
entries are not automatically reclaimed after ownership changes; reliable replay/idempotency is a
separate delivery concern.

#### Standby Health Check

The `KnxConnectionHealthCheck` tracks an `IsLeader` flag alongside the existing `ConnectionActive` flag. This prevents standby replicas from being marked unhealthy (and restarted by Kubernetes) while they are legitimately waiting for the distributed lock:

| `IsLeader` | `ConnectionActive` | Health status |
| --- | --- | --- |
| `false` | — | **Healthy** — standby, waiting for distributed lock |
| `true` | `true` | **Healthy** — bus connection online |
| `true` | `false` | **Unhealthy** — leader has lost bus connectivity |

Cancellation immediately resets `IsLeader` and `ConnectionActive`, returning the replica to
standby-healthy status while cleanup finishes. Fatal run failures and disposal also clear them.
An empty connection map is not considered connected.

### Telegram Filtering

The filters applied in `Bus_GroupMessageReceived` depend on the configured `ServiceFamily`.

#### Tunneling mode

When running with multiple bus connections, two filters are applied before processing a telegram:

1. **Line affinity** — only process a telegram if the source device's area/line matches the receiving bus connection's area/line. KNX line couplers route telegrams across lines, which means every connection receives cross-line traffic. This cheap integer comparison prevents N−1 duplicate publishes per routed telegram.

2. **Rival tunneling address** — during discovery, all tunneling slot individual addresses are recorded. When a telegram arrives whose source is a discovered tunneling address but *not* one of our active connections, it originated from another deployment sharing the same KNX interfaces (e.g. production on slot 1, dev on slot 2) and is dropped.

#### Routing mode

In routing mode a single multicast connection receives all group telegrams from the entire KNX backbone. Neither the line-affinity nor the rival-tunneling-address filter applies — the bus connection's individual address is the router's backbone address, not a TP device, so source area/line comparisons would incorrectly drop every incoming telegram. Both filters are therefore skipped when `ServiceFamily` is `Routing`.

## Event Flow

```mermaid
flowchart TD
    BUS["KNX Bus\n(KNXnet/IP Tunnelling or Routing)"]

    subgraph Monitor["KnxMonitorBgService"]
        LOOKUP["Load group address XML"]
        LOG_CONFIG["Log ServiceFamily &\nShardingMode"]
        REDLOCK{"Shared leadership runner\n(Unified mode)"}
        DISCOVER["Discover KNX IP devices"]
        CONNECT["Connect per Area/Line\nor join multicast"]
        RECEIVE["Bus_GroupMessageReceived"]
        SVC_CHECK{"Tunneling?"}
        FILTER_LINE{"Line affinity\nfilter"}
        FILTER_RIVAL{"Rival address\nfilter"}
        DECODE["Decode value & build KnxEvent"]
        PUBLISH["Ownership-local buffer\nawaited publisher"]
    end

    CHANNEL_IN["IKnxTelegramBroker&lt;KnxEvent&gt;\n(Channel or Redis)"]

    subgraph Processor["KnxProcessorBgService"]
        READ["ReadAllAsync"]
        DISPATCH["Fan-out to all sinks\nTask.WhenAll"]
    end

    SINK_CONSOLE["Console Sink\n(Serilog)"]
    SINK_REDIS["Redis Sink\n(latest state)"]
    SINK_AZTABLES["Azure Tables Sink\n(history + snapshot)"]
    SINK_CEMI["Azure CEMI Tables Sink\n(raw frames, batched)"]
    SINK_OTEL["OpenTelemetry Sink\n(metrics)"]

    CHANNEL_OUT["IKnxTelegramBroker&lt;KnxOutgoingTelegram&gt;\n(Channel or Redis)"]

    subgraph Sender["KnxSenderBgService"]
        SEND["Dequeue & write to bus"]
    end

    LOOKUP --> LOG_CONFIG --> REDLOCK --> DISCOVER --> CONNECT
    BUS -->|GroupEventArgs| RECEIVE
    CONNECT --> RECEIVE --> SVC_CHECK
    SVC_CHECK -->|yes| FILTER_LINE
    SVC_CHECK -->|routing| DECODE
    FILTER_LINE -->|same line| FILTER_RIVAL
    FILTER_LINE -.->|cross-line| DROP1(("drop"))
    FILTER_RIVAL -->|our address| DECODE
    FILTER_RIVAL -.->|rival address| DROP2(("drop"))
    DECODE --> PUBLISH --> CHANNEL_IN
    CHANNEL_IN --> READ --> DISPATCH
    DISPATCH --> SINK_CONSOLE
    DISPATCH --> SINK_REDIS
    DISPATCH --> SINK_AZTABLES
    DISPATCH --> SINK_CEMI
    DISPATCH --> SINK_OTEL
    CHANNEL_OUT --> SEND --> BUS
    CONNECT -.->|same ownership token| SEND
```

## Configuration Examples

### Minimal

```json
{
  "CasCap": {
    "KnxConfig": {
      "ServiceFamily": "Tunneling",
      "GroupAddressXmlFilePath": "/etc/knx/knxgroupaddresses.xml",
      "TunnelingAreaLineFilter": ["1.1"],
      "DayNightTimeZoneLocation": "Berlin",
      "DayNightLatitude": 52.520008,
      "DayNightLongitude": 13.404954,
      "AzureTableStorageConnectionString": "https://<account>.table.core.windows.net",
      "Sinks": {
        "AvailableSinks": {
          "Console": { "Enabled": true },
          "Metrics": { "Enabled": true }
        }
      }
    }
  }
}
```

### Fully configured

```json
{
  "CasCap": {
    "KnxConfig": {
      "ServiceFamily": "Tunneling",
      "ShardingMode": "Unified",
      "HealthCheck": "Readiness",
      "GroupAddressXmlFilePath": "/etc/knx/knxgroupaddresses.xml",
      "TunnelingAreaLineFilter": ["1.1", "1.2", "1.3"],
      "StateChangeAlerts": {"SYS-[RM_Alarm_FB]": "Smoke detector alarm"},
      "DayNightEnabled": true,
      "DayNightTimeZoneLocation": "Berlin",
      "DayNightLatitude": 52.520008,
      "DayNightLongitude": 13.404954,
      "DayNightGroupAddressName": "SYS-[Night_Day]",
      "DayNightSunriseHourOverrideWeekday": 6,
      "DayNightSunriseHourOverrideWeekend": 7,
      "FrontDoorContactGroupName": "EG-BI-Entrance(FrontDoor)-East-STATE",
      "FrontDoorLightGroupName": "EG-LI-Entrance(FrontDoor)-Outdoor-DL",
      "OfficeLightGroupName": "DG-LI-Office-DL-South",
      "BusSenderLoggingEnabled": true,
      "StateChangePollingDelayMs": 100,
      "StateChangeMaxPollIterations": 20,
      "QueuePollingDelayMs": 50,
      "QueueMaxConcurrency": 5,
      "DiscoveryRetryDelayMs": 10000,
      "ConnectionPollingDelayMs": 1000,
      "ConnectionLogEscalationInterval": 10,
      "ConnectionHealthPollingDelayMs": 1000,
      "ReconnectBackoffMs": 1000,
      "ReconnectMaxBackoffMs": 60000,
      "AzureTableStorageConnectionString": "https://example.table.core.windows.net",
      "TelegramBrokerMode": "Redis",
      "TelegramConsumerGroupStartId": "0",
      "Sinks": {
        "AvailableSinks": {
          "Console": { "Enabled": true },
          "Metrics": { "Enabled": true }
        }
      },
      "Translations": {
        "de": {
          "Floors": { "DG": "Dachgeschoss", "OG": "Obergeschoss", "EG": "Erdgeschoss", "KG": "Keller" },
          "Rooms": { "Kitchen": "Küche", "Office": "Büro", "LivingRoom": "Wohnzimmer" },
          "Orientations": { "North": "Nord", "East": "Ost", "South": "Süd", "West": "West" },
          "Categories": { "LI": "Beleuchtung", "HZ": "Heizung", "BL": "Rollladen", "SD": "Steckdose" }
        }
      }
    }
  }
}
```

## License

This project is released under the [MIT License](https://github.com/f2calv/SmartHaus/blob/main/LICENSE).
