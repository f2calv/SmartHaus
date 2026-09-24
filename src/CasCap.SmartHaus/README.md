# CasCap.SmartHaus

The central ASP.NET Core library that hosts the consolidated [SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction) hub, coordinates real-time event broadcasting across all home automation features, and provides supporting services for AI agents, dynamic DNS, and Signal messenger notifications.

## Purpose

### SignalR Hub — `HausHub`

`HausHub` is an `[Authorize]` SignalR hub mounted at `/hubs/haus` (configurable via `SignalRHubConfig.HubPath`). It implements `IHausServerHub` and broadcasts the four core event types to all connected clients:

| Server method | Payload type | Description |
| --- | --- | --- |
| `SendFroniusEvent(e)` | `FroniusEvent` | Broadcasts a solar inverter reading |
| `SendKnxTelegram(e)` | `KnxEvent` | Broadcasts a KNX bus telegram |
| `SendDoorBirdEvent(e)` | `DoorBirdEvent` | Broadcasts a door station event |
| `SendBuderusEvent(e)` | `BuderusEvent` | Broadcasts a heating system reading |
| `SendMessage(user, message, date)` | — | Broadcasts a text message to all other clients |
| `Broadcast(message)` | — | Broadcasts a text message to all clients (including sender) |

After broadcasting, each event is also written to the server-side `IEventSink<HubEvent>` implementations registered in `SignalRHubConfig.Sinks`.

### Client-Side Event Sinks (forward to hub)

These sinks are registered in the feature pods and forward domain events to the hub:

| Sink | Forwards |
| --- | --- |
| `FroniusSinkSignalRService` | `FroniusEvent` → `SendFroniusEvent` |
| `KnxSinkSignalRService` | `KnxEvent` → `SendKnxTelegram` |
| `DoorBirdSinkSignalRService` | `DoorBirdEvent` → `SendDoorBirdEvent` |
| `BuderusSinkSignalRService` | `BuderusEvent` → `SendBuderusEvent` |

### Hub-Side Event Sinks (server process)

| Sink | Description |
| --- | --- |
| `HausHubSinkConsoleService` | Logs every `HubEvent` via the .NET logger |
| `HausHubSinkMetricsService` | Records OpenTelemetry metrics per `HubEvent` type |
| `CommsStreamSinkService` | Writes/reads `CommsEvent` entries to/from the Redis Stream configured by `CommsAgentConfig.StreamKey` |
| `MediaStreamSinkService` | Writes/reads `MediaEvent` entries to/from the Redis Stream configured by `MediaConfig.StreamKey` |

### Background Services

| Service | Description |
| --- | --- |
| `CommunicationsBgService` | Gateway agent — consumes the comms Redis Stream and incoming Signal messages, routes both through CommsAgent, and relays responses to the Signal notification group. Voice attachments are transcribed through `IVoiceTranscriptionService` before CommsAgent sees them, and only the transcript is forwarded. Posts debug notifications to `PhoneNumberDebug` for delegation events, completion events, and session compaction events |
| `MediaBgService` | Consumes the media Redis Stream (`MediaConfig.StreamKey`), routes media to the domain agent configured in `MediaConfig.SourceAgentMap` (e.g. DoorBird → SecurityAgent), and posts analysis findings back to the comms stream. Runs in the Comms pod alongside `CommunicationsBgService` |
| `HausHubSinksBgService` | Initialises the hub-side `IEventSink<HubEvent>` implementations |
| `FroniusSymoSignalRClientService` | Connects to the hub as a SignalR client |
| `UbiquitiBgService` | Ubiquiti network integration (planned) |

### REST API

| Endpoint | Auth | Description |
| --- | --- | --- |
| `GET /api/system` | Required | Returns git build metadata (`GitMetadata`) |

## Configuration

### `SignalRHubConfig` (`CasCap:SignalRHubConfig`)

| Setting | Type | Default | Description |
| --- | --- | --- | --- |
| `HubPath` | `string` | `"/hubs/haus"` | URL path at which the hub is mounted |
| `Sinks.AvailableSinks` | `Dictionary<string, SinkConfigParams>` | `Console=true, Metrics=true` | Hub-side event sinks to enable |
| `ConsoleLogIntervalMs` | `int` | `30000` | Logging interval in milliseconds for the console sink periodic event count output |
| `MetricsBatchSize` | `int` | `10` | Number of events to accumulate before flushing to the OpenTelemetry counter |
| `MetricsFlushIntervalMs` | `int` | `60000` | Periodic flush interval in milliseconds for the metrics sink |

### `CommsAgentConfig` (`CasCap:AIConfig:Agents:CommsAgent:Settings`)

| Setting | Type | Default | Description |
| --- | --- | --- | --- |
| `GroupName` | `string` | — | The name of the Signal group used for notifications |
| `StreamKey` | `string` | `"comms:stream:events"` | Redis Stream key for cross-instance communication of key events |
| `ConsumerGroup` | `string` | `"comms:agents"` | Redis consumer group name |
| `ConsumerName` | `string` | `"comms-0"` | Consumer name within the group |
| `ConsumerGroupStartId` | `string` | `"0"` | Starting ID when creating the consumer group (`"0"` = from beginning, `"$"` = new only) |
| `StreamReadPosition` | `string` | `">"` | Read position passed to `XREADGROUP` |
| `StreamReadCount` | `int` | `10` | Maximum entries per `XREADGROUP` call |
| `PollingIntervalMs` | `int` | `5000` | Polling interval for comms stream and REST message retrieval |
| `HealthCheckProbeDelayMs` | `int` | `2000` | Delay in milliseconds between signal-cli readiness probes at startup |
| `FlushTimeoutMs` | `int` | `5000` | Timeout in milliseconds for flushing pending envelopes at startup |

### `MediaConfig` (`CasCap:MediaConfig`)

| Setting | Type | Default | Description |
| --- | --- | --- | --- |
| `SourceAgentMap` | `Dictionary<string, string>` | — | Maps event source names (e.g. `"DoorBird"`) to agent keys (e.g. `"SecurityAgent"`) for media analysis routing |
| `StreamKey` | `string` | `"media:stream:events"` | Redis Stream key for source-agnostic media events |
| `ConsumerGroup` | `string` | `"media:processors"` | Redis consumer group name |
| `ConsumerName` | `string` | `"media-0"` | Consumer name within the group |
| `ConsumerGroupStartId` | `string` | `"0"` | Starting ID when creating the consumer group (`"0"` = from beginning, `"$"` = new only) |
| `StreamReadPosition` | `string` | `">"` | Read position passed to `XREADGROUP` |
| `StreamReadCount` | `int` | `10` | Maximum entries per `XREADGROUP` call |
| `PollingIntervalMs` | `int` | `1000` | Polling interval for the media stream consumer |

### `SecurityAgentConfig` (`CasCap:AIConfig:Agents:SecurityAgent:Settings`)

| Setting | Type | Default | Description |
| --- | --- | --- | --- |
| `ImageCacheKeyPrefix` | `string` | — | Redis key prefix for cached image bytes |
| `ImageCacheTtlMs` | `int` | `300000` | Time-to-live in milliseconds for cached image bytes in Redis |

### `HeatingAgentConfig` (`CasCap:AIConfig:Agents:HeatingAgent:Settings`)

| Setting | Type | Default | Description |
| --- | --- | --- | --- |
| `Dhw1AlertHysteresis` | `double` | `1.0` | Hysteresis in °C for the DHW1 setpoint alert |
| `Dhw1AlertCooldownMs` | `int` | `3600000` | Minimum cooldown in milliseconds between consecutive DHW1 setpoint alerts |

### Voice configuration (`CasCap.Api.Voice`)

Speech-to-text and text-to-speech processing is owned by the adjacent `CasCap.Api.Voice` library and
registered through its DI extensions. SmartHaus supplies the application configuration and retains
only communications orchestration such as `EchoTranscriptToDebugChat`.

### `SpeechToTextConfig` (`CasCap:SpeechToTextConfig`)

Every setting has a safe default, so the section may be omitted entirely. The provider-specific
endpoints are nullable and are read only when that provider is selected.

| Setting | Type | Default | Description |
| --- | --- | --- | --- |
| `Mode` | `VoiceProcessingMode` | `Disabled` | `Disabled` rejects voice without downloading, `Shadow` transcribes for measurement without replying, `Enabled` drives a normal text turn |
| `Provider` | `SpeechToTextProvider` | `WhisperAsr` | Which backend transcribes: `WhisperAsr`, `WhisperCpp` or `Azure` |
| `WhisperAsrEndpoint` | `string` | `http://localhost:9000` | Base address of the openai-whisper-asr-webservice deployment |
| `WhisperCppEndpoint` | `string?` | `null` | Base address of the whisper.cpp `whisper-server` deployment; required for `WhisperCpp` |
| `AzureEndpoint` | `string?` | `null` | Azure AI Speech resource endpoint; required for `Azure`. Authenticates with the ambient token credential, so no key is stored |
| `AzureLocales` | `string[]?` | `null` | Candidate locales such as `en-GB`. Empty lets the multilingual model identify the language itself. Azure requires a full locale, not the bare code in `Language` |
| `Language` | `string` | `en` | ISO 639-1 code passed to the whisper backends |
| `ModelId` | `string?` | `null` | Optional model identifier reported alongside a transcription |
| `TimeoutMs` | `int` | `120000` | Total budget for one transcription, including admission and conversion |
| `MaxCompressedBytes` | `int` | `5242880` | Largest accepted attachment before any conversion |
| `MaxDecodedBytes` | `int` | `19200000` | Largest accepted decoded WAV, about 10 minutes of 16 kHz mono PCM |
| `MaxDurationSeconds` | `int` | `300` | Longest accepted recording |
| `FfmpegPath` | `string` | `ffmpeg` | ffmpeg executable used to normalise non-WAV audio |

## Agent Integration — Signal Messenger

### CommsAgent — the gateway agent

`CommunicationsBgService` is the **sole component that communicates with Signal**. It acts as a gateway between the smart home and the user:

1. **Comms stream** — Consumes `CommsEvent` entries from the Redis Stream configured by `CommsAgentConfig.StreamKey` (default `comms:stream:events`). These are published by feature-pod sinks (KNX state changes, Fronius SOC alerts, DDNS changes) and by `MediaBgService` (analysis results from domain agents such as SecurityAgent).
2. **Incoming messages** — Polls the signal-cli REST API for new Signal group messages.
3. **Agent routing** — Routes both stream events and incoming user messages through the `CommsAgent` (`AIAgent` resolved from `AIConfig.Agents[AgentKeys.CommsAgent]`), which decides how to respond.
4. **Outbound** — Sends the agent's response to the Signal notification group via `INotifier`.

Domain agents (SecurityAgent, HeatingAgent, etc.) **never talk to Signal directly**. They publish their findings to the comms stream, and CommsAgent relays, aggregates, or suppresses notifications as appropriate.

### Media pipeline — source-agnostic media analysis (Comms pod)

`MediaBgService` runs alongside `CommunicationsBgService` in the Comms pod and provides a dedicated pipeline for binary media (images, audio, documents):

1. **Media stream** — Consumes `MediaEvent` entries from the Redis Stream configured by `MediaConfig.StreamKey` (default `media:stream:events`), published by source-specific sinks (e.g. `DoorBirdSinkMediaStreamService`).
2. **Agent routing** — Looks up `MediaConfig.SourceAgentMap` to find the domain agent for the event source (e.g. `"DoorBird" → "SecurityAgent"`).
3. **Analysis** — Fetches cached media bytes from Redis and runs the domain agent (e.g. a vision-capable SecurityAgent) against them.
4. **Findings** — Posts the analysis result as a `CommsEvent` to `comms:stream:events` with a `MediaReference` in `JsonPayload` (pointing to the cached image bytes), where CommunicationsBgService picks it up, fetches the image from Redis, and relays both text and image to the Signal group.

This enables users to interact with the smart home AI assistant directly from the Signal mobile app, eliminating the need for a custom mobile application.

Each agent's orchestration settings live in a `Settings` sub-section under the corresponding `CasCap:AIConfig:Agents:{key}` entry in `appsettings.json`, bound to a strongly-typed record (e.g. `CommsAgentConfig`, `SecurityAgentConfig`, `HeatingAgentConfig`). The dictionary key doubles as the agent identifier — `AgentKeys` provides compile-time constants for all well-known agent names.

### Audio attachment flow — speech-to-text transcription

When a user sends an audio clip (e.g. a voice message) via Signal, `CommunicationsBgService` intercepts it before the comms agent sees it:

1. **Download** — The attachment bytes and MIME type (`audio/aac`, `audio/ogg`, etc.) are downloaded from signal-cli. Every attachment identifier on the envelope is then deleted, whether or not it was selected.
2. **Validate** — the default `IVoiceTranscriptionService` implementation checks the declared media type against the payload's own file signature and enforces the configured compressed-size, decoded-size and duration limits. Nothing is transmitted until those pass.
3. **Normalise** — Audio that is not already 16 kHz mono signed 16-bit PCM WAV is piped through `ffmpeg` (stdin to stdout), so it never touches the file system.
4. **Transcribe** — The WAV is handed to an `ISpeechToTextClient`, the `Microsoft.Extensions.AI` abstraction. Which implementation runs is chosen by `CasCap:SpeechToTextConfig:Provider`; see [Speech-to-text providers](#speech-to-text-providers) below. Switching provider is a configuration change, not a code change.
5. **Inject** — Only the normalised transcript reaches CommsAgent, which processes it as ordinary text. Raw audio is never forwarded, and a failed transcription produces one concise reply with no agent turn and nothing persisted to the conversation.

`CasCap:SpeechToTextConfig:Mode` gates the whole path: `Disabled` rejects voice without downloading, `Shadow` transcribes and cleans up for measurement without replying, and `Enabled` drives a normal text turn.

```mermaid
flowchart LR
    SIGNAL(["Signal<br/>voice message"]) -->|audio/aac bytes| DOWNLOAD["Download<br/>attachment"]
    DOWNLOAD --> TRANSCODE["ffmpeg<br/>AAC → WAV<br/>(16kHz mono PCM)"]
    TRANSCODE --> STT(("ISpeechToTextClient<br/>(selected provider)"))
    STT -->|transcribed text| INJECT["Replace the prompt<br/>with the transcript"]
    INJECT --> COMMS(("CommsAgent"))
    COMMS -->|response| SIGNAL
```

### Speech-to-text providers

Three interchangeable implementations of `ISpeechToTextClient` are shipped. `Provider` selects one; the
others stay dormant and resolve no configuration or credentials.

| Provider | Implementation | Transport |
| --- | --- | --- |
| `WhisperAsr` | `WhisperAsrSpeechToTextClient` | multipart `POST /asr`, part `audio_file`, with `task`, `language`, `encode` and `output` query parameters. `encode=false` for WAV so the server skips its own ffmpeg pass |
| `WhisperCpp` | `WhisperCppSpeechToTextClient` | multipart `POST /inference`, part `file`, with `response_format` and `language` form fields |
| `Azure` | `AzureSpeechToTextClient` | Azure AI Speech fast transcription, via `ISpeechService` in `CasCap.Api.Azure.CognitiveServices` |

The route and the multipart part name differ between the two whisper servers, which is why they are
separate adapters rather than one adapter with a mode flag.

#### Measured comparison

All figures are a single voice message through the full pipeline on the same hardware. *Realtime* is
seconds of audio per second of wall clock, so above `1.0` is faster than playback.

| Provider | Model | Transcribe | Realtime | ms per audio second |
| --- | --- | --- | --- | --- |
| `WhisperAsr` (`openai_whisper`) | small | 14,847 ms | 0.2x | 4,013 |
| `WhisperAsr` (`faster_whisper`) | small | 7,881 ms | 0.4x | 2,627 |
| `WhisperCpp` (Vulkan GPU) | small | ~7,000 ms | ~0.4x | ~2,333 |
| `Azure` | fast transcription | **718 ms** | **5.6x** | **180** |

| Provider | Advantages | Disadvantages |
| --- | --- | --- |
| `WhisperAsr` | Audio never leaves the network. No account, key or quota. Swappable engine and model through its own environment variables | Slowest. CPU-bound, and competes with every other workload on the node |
| `WhisperCpp` | Audio never leaves the network. Can offload to a GPU. Smallest runtime footprint | Needs a GPU to be worth running, and the published arm64 images do not execute on every Arm CPU, so an image build may be required |
| `Azure` | By far the fastest, and the only one whose cost scales with the length of the recording. Best accuracy observed. No local compute at all | Audio leaves the network. Needs an Azure resource, a credential and a role assignment. Per-transaction cost, and a quota |

The whisper figures are dominated by a design detail rather than the hardware: Whisper pads every
recording to a fixed 30-second window, so a three-second message costs the same as a thirty-second
one. That is why the realtime factor stays below `1.0` no matter how briefly you speak, and why only
the Azure figure improves with shorter audio.

Speed is not the only axis. Both whisper providers keep household audio on the local network, which
may outweigh latency once a voice message can come from someone other than the operator.

## Service Architecture

```mermaid
flowchart TD
    subgraph FeaturePods["Feature pods (Fronius / KNX / DoorBird / Buderus)"]
        FRONIUS_SINK["FroniusSinkSignalRService"]
        KNX_SINK["KnxSinkSignalRService"]
        DOORBIRD_SINK["DoorBirdSinkSignalRService"]
        BUDERUS_SINK["BuderusSinkSignalRService"]
        FRONIUS_COMMS["FroniusSinkCommsStreamService"]
        KNX_COMMS["KnxSinkCommsStreamService"]
        DOORBIRD_MEDIA["DoorBirdSinkMediaStreamService"]
    end

    subgraph Hub["CasCap.SmartHaus (HausHub @ /hubs/haus)"]
        HAUSHUB["HausHub\n[Authorize]"]
        HUB_CONSOLE["HausHubSinkConsoleService"]
        HUB_METRICS["HausHubSinkMetricsService"]
    end

    subgraph Comms["CasCap.SmartHaus (Comms instance — gateway + media analysis)"]
        COMMS_BG["CommunicationsBgService"]
        COMMS_AGENT(("CommsAgent"))
        STT(("Speech-to-text\n(selected provider)"))
        MEDIA_BG["MediaBgService"]
        SECURITY_AGENT(("SecurityAgent\n(vision)"))
    end

    MEDIA_STREAM[("Redis Stream\nMediaConfig.StreamKey")]
    COMMS_STREAM[("Redis Stream\nCommsAgentConfig.StreamKey")]
    REDIS_CACHE[("Redis\nimage cache")]
    CLIENTS["SignalR clients\n(MAUI app, browser, etc.)"]
    SIGNALCLI["Signal messenger\n(signal-cli REST API)"]

    %% SignalR path
    FRONIUS_SINK -->|SendFroniusEvent| HAUSHUB
    KNX_SINK -->|SendKnxTelegram| HAUSHUB
    DOORBIRD_SINK -->|SendDoorBirdEvent| HAUSHUB
    BUDERUS_SINK -->|SendBuderusEvent| HAUSHUB
    HAUSHUB -->|ReceiveFroniusEvent etc.| CLIENTS
    HAUSHUB --> HUB_CONSOLE
    HAUSHUB --> HUB_METRICS

    %% Comms stream path (text events)
    FRONIUS_COMMS -->|CommsEvent| COMMS_STREAM
    KNX_COMMS -->|CommsEvent| COMMS_STREAM

    %% Media stream path (binary media)
    DOORBIRD_MEDIA -->|cache bytes| REDIS_CACHE
    DOORBIRD_MEDIA -->|MediaEvent| MEDIA_STREAM
    MEDIA_STREAM --> MEDIA_BG
    MEDIA_BG -->|fetch bytes| REDIS_CACHE
    MEDIA_BG --> SECURITY_AGENT
    SECURITY_AGENT -->|analysis CommsEvent| COMMS_STREAM

    %% CommsAgent gateway
    COMMS_STREAM --> COMMS_BG
    COMMS_BG -->|audio attachment| STT
    STT -->|transcript| COMMS_BG
    COMMS_BG --> COMMS_AGENT
    COMMS_AGENT -->|relay to group| SIGNALCLI
    SIGNALCLI -->|incoming messages| COMMS_BG
```

## Agent Instructions

Agent instruction markdown files are compiled as embedded resources in this project and resolved at runtime by `AgentExtensions.ResolveInstructions` in `CasCap.Common.AI`. To update an agent's behaviour, edit the corresponding file and redeploy.

## MCP Query Services

MCP query services registered by `HausMcpServiceCollectionExtensions` expose domain tools and prompts to AI agents. Each service is conditionally registered based on enabled features.

| Service | Tools | Prompts | Domain |
| --- | --- | --- | --- |
| `SystemMcpQueryService` | 3 | — | System-level tools available to all agents (date/time, provider list, agent list) |
| `BusSystemMcpQueryService` | 21 | 5 | Bus system — door/window contacts, door locks, shutters, HVAC, power outlets, diagnostics |
| `HeatPumpMcpQueryService` | 2 | 5 | Heat pump |
| `InverterMcpQueryService` | 7 | 5 | Solar inverter |
| `FrontDoorMcpQueryService` | 8 | 5 | Front door intercom |
| `AppliancesMcpQueryService` | 9 | 5 | Home appliances |
| `EdgeHardwareMcpQueryService` | 1 | — | Edge hardware monitoring (GPU/CPU metrics) |
| `IpCameraMcpQueryService` | 1 | — | IP cameras (UniFi Protect event status) |
| `AquariumMcpQueryService` | 2 | — | Aquarium water pump (Sicce) |
| `SmartPlugMcpQueryService` | 3 | — | Smart plugs (Shelly) |
| `SmartLightingMcpQueryService` | 15 | — | Lighting — KNX ceiling/wall lights and Wiz smart bulbs |
| `MessagingMcpQueryService` | 3 | — | Signal messaging polls (create, close, status) |

### MCP Registration

Register individually per feature flag (as done in `Program.cs`):

```csharp
services.AddSystemMcp();
services.AddBusSystemMcp();
services.AddHeatPumpMcp();
services.AddInverterMcp();
services.AddFrontDoorMcp();
services.AddAppliancesMcp();
services.AddEdgeHardwareMcp();
services.AddCamerasMcp();
services.AddAquariumMcp();
services.AddSmartPlugMcp();
services.AddSmartLightingMcp();
services.AddMessagingMcp(phoneNumber, groupName);
```

### MCP Service Architecture

```mermaid
graph TD
    classDef system fill:#e0f2fe,stroke:#0284c7,color:#0c4a6e
    classDef integration fill:#fef3c7,stroke:#f59e0b,color:#78350f
    classDef core fill:#dbeafe,stroke:#3b82f6,color:#1e3a8a

    REG["Program.cs<br/>(feature-gated registration)"]:::core

    subgraph System["System Tools"]
        SYS["SystemMcpQueryService<br/>(3 tools)"]:::system
    end

    subgraph HomeAutomation["Home Automation"]
        BUS["BusSystemMcpQueryService<br/>(21 tools, 5 prompts)"]:::integration
        HEAT["HeatPumpMcpQueryService<br/>(2 tools, 5 prompts)"]:::integration
        INVERTER["InverterMcpQueryService<br/>(7 tools, 5 prompts)"]:::integration
        DOOR["FrontDoorMcpQueryService<br/>(8 tools, 5 prompts)"]:::integration
        APPLIANCES["AppliancesMcpQueryService<br/>(9 tools, 5 prompts)"]:::integration
        CAMERAS["IpCameraMcpQueryService<br/>(1 tool)"]:::integration
        AQUARIUM["AquariumMcpQueryService<br/>(2 tools)"]:::integration
        PLUGS["SmartPlugMcpQueryService<br/>(3 tools)"]:::integration
        LIGHTS["SmartLightingMcpQueryService<br/>(15 tools)"]:::integration
    end

    subgraph Platform["Platform Services"]
        EDGE["EdgeHardwareMcpQueryService<br/>(1 tool)"]:::system
        MSG["MessagingMcpQueryService<br/>(3 tools)"]:::system
    end

    REG --> SYS
    REG --> BUS
    REG --> HEAT
    REG --> INVERTER
    REG --> DOOR
    REG --> APPLIANCES
    REG --> CAMERAS
    REG --> AQUARIUM
    REG --> PLUGS
    REG --> LIGHTS
    REG --> EDGE
    REG --> MSG

    BUS -.uses.-> KNX["CasCap.Api.Knx"]
    HEAT -.uses.-> BUDERUS["CasCap.Api.Buderus"]
    INVERTER -.uses.-> FRONIUS["CasCap.Api.Fronius"]
    DOOR -.uses.-> DOORBIRD["CasCap.Api.DoorBird"]
    APPLIANCES -.uses.-> MIELE["CasCap.Api.Miele"]
    CAMERAS -.uses.-> UBIQUITI["CasCap.Api.Ubiquiti"]
    AQUARIUM -.uses.-> SICCE["CasCap.Api.Sicce"]
    PLUGS -.uses.-> SHELLY["CasCap.Api.Shelly"]
    LIGHTS -.uses.-> WIZ["CasCap.Api.Wiz"]
```

## Agent Architecture

How agents delegate to sub-agents and consume tool services:

```mermaid
flowchart TD
    classDef orchestrator fill:#dbeafe,stroke:#3b82f6,color:#1e3a8a
    classDef specialist fill:#d1fae5,stroke:#10b981,color:#064e3b
    classDef disabled fill:#f3f4f6,stroke:#9ca3af,color:#6b7280,stroke-dasharray:5 5
    classDef shared fill:#fef3c7,stroke:#f59e0b,color:#78350f
    classDef stt fill:#ede9fe,stroke:#8b5cf6,color:#4c1d95
    classDef unassigned fill:#fee2e2,stroke:#ef4444,color:#991b1b

    Comms(["CommsAgent<br/>(orchestrator)"]):::orchestrator

    Security["SecurityAgent"]:::specialist
    Heating["HeatingAgent"]:::specialist
    Energy["EnergyAgent"]:::specialist
    HomeControl["HomeControlAgent"]:::specialist
    Infra["InfraAgent"]:::specialist
    Appliances["AppliancesAgent<br/>(disabled)"]:::disabled
Audio["Speech-to-text<br/>(selected provider)"]:::stt

    Comms -->|delegates| Security
    Comms -->|delegates| Heating
    Comms -->|delegates| Energy
    Comms -->|delegates| HomeControl
    Comms -->|delegates| Infra
    Comms -->|delegates| Appliances
    Comms -.->|audio STT| Audio

    subgraph SharedSvc["Shared Services"]
        SYS["SystemMcpQueryService<br/>get_current_datetime_state · get_providers · get_agents"]:::shared
        MSG["MessagingMcpQueryService<br/>create_poll · close_poll · get_poll_status"]:::shared
    end

    Comms -.-> SharedSvc
    Security -.-> SharedSvc
    Heating -.-> SharedSvc
    Energy -.-> SharedSvc
    HomeControl -.-> SharedSvc
    Infra -.-> SharedSvc
    Appliances -.-> SharedSvc

    subgraph AudioPipeline["Audio Transcription"]
        AUDIO_IN["audio/aac bytes"] --> FFMPEG["ffmpeg<br/>AAC → WAV<br/>(16kHz mono PCM)"]
        FFMPEG --> WHISPER["ISpeechToTextClient<br/>(selected provider)"]
        WHISPER --> TRANSCRIPTION["transcribed text"]
    end
    Audio --> AudioPipeline

    subgraph FrontDoor["FrontDoorMcpQueryService (8 tools)"]
        FD_state["get_house_door_state"]
        FD_photo["get_house_door_photo"]
        FD_info["get_house_door_photo_info"]
        FD_unlock["unlock_house_door"]
        FD_night["enable_house_door_night_vision"]
        FD_video["get_house_door_video_stream_url"]
        FD_hist["get_house_door_history_image"]
        FD_histInfo["get_house_door_history_image_info"]
    end
    Security --> FrontDoor

    subgraph SecurityBus["BusSystemMcpQueryService (SecurityAgent)"]
        SB_door["get_house_front_door_state"]
    end
    Security --> SB_door

    subgraph SecurityLights["SmartLightingMcpQueryService (SecurityAgent)"]
        SL_doorOn["turn_on_house_door_light"]
        SL_doorOff["turn_off_house_door_light"]
    end
    Security --> SL_doorOn
    Security --> SL_doorOff

    subgraph CommsBus["BusSystemMcpQueryService (CommsAgent)"]
        CB_rooms["get_house_rooms"]
        CB_floors["get_house_floors"]
    end
    Comms --> CB_rooms
    Comms --> CB_floors

    subgraph HeatPump["HeatPumpMcpQueryService"]
        HP_state["get_heat_pump_state"]
        HP_set["set_heat_pump_data_point"]
    end
    Heating --> HeatPump

    subgraph KnxHvac["Remote: mcp/knx (heating zones)"]
        KH_change["change_house_heating_zone"]
        KH_zones["get_house_heating_zones"]
        KH_zone["get_house_heating_zone"]
    end
    Heating --> KnxHvac

    subgraph Inverter["InverterMcpQueryService (7 tools)"]
        INV_flow["get_inverter_power_flow"]
        INV_elec["get_inverter_electrical_readings"]
        INV_info["get_inverter_info"]
        INV_devices["get_inverter_connected_devices"]
        INV_meter["get_inverter_meter_readings"]
        INV_battery["get_inverter_battery_status"]
        INV_snap["get_inverter_snapshot"]
    end
    Energy --> Inverter

    subgraph BusHome["BusSystemMcpQueryService (HomeControl, 18 tools)"]
        BH_note["shutters · outlets · rooms · floors<br/>diagnostics · front door state<br/>(excludes 3 heating zone tools)"]
    end
    HomeControl --> BusHome

    subgraph LightsHome["SmartLightingMcpQueryService (HomeControl, all 15 tools)"]
        LH_note["KNX ceiling/wall lights · WiZ smart bulbs<br/>on/off · status · all-on/all-off"]
    end
    HomeControl --> LightsHome

    subgraph EdgeHW["EdgeHardwareMcpQueryService"]
        EDGE_snap["get_edge_hardware_snapshots"]
    end
    Infra --> EdgeHW

    subgraph AppSvc["AppliancesMcpQueryService (9 tools)"]
        APP_all["get_all_appliances · summary"]
        APP_detail["get_appliance · identification · state · actions"]
        APP_exec["execute_appliance_action · get/start_programs"]
    end
    Appliances --> AppSvc

    subgraph Unassigned["Unassigned Services"]
        UA_cam["IpCameraMcpQueryService · 1 tool"]:::unassigned
        UA_plug["SmartPlugMcpQueryService · 3 tools"]:::unassigned
        UA_aqua["AquariumMcpQueryService · 2 tools"]:::unassigned
    end
```

### Agent Tools Summary

| Agent | Direct Tools | Via Delegation | Total |
| --- | --- | --- | --- |
| SecurityAgent | 17 | — | 17 |
| HeatingAgent | 11 | — | 11 |
| EnergyAgent | 13 | — | 13 |
| HomeControlAgent | 37 | — | 37 |
| InfraAgent | 7 | — | 7 |
| AppliancesAgent | 15 | — | 15 |
| CommsAgent | 8 | 100 | 108 |

## Agent Instruction Files

| Agent | Instruction file |
| --- | --- |
| SecurityAgent | [SecurityAgent.instructions.md](Resources/SecurityAgent.instructions.md) |
| HeatingAgent | [HeatingAgent.instructions.md](Resources/HeatingAgent.instructions.md) |
| EnergyAgent | [EnergyAgent.instructions.md](Resources/EnergyAgent.instructions.md) |
| HomeControlAgent | [HomeControlAgent.instructions.md](Resources/HomeControlAgent.instructions.md) |
| CommsAgent | [CommsAgent.instructions.md](Resources/CommsAgent.instructions.md) |
| InfraAgent | [InfraAgent.instructions.md](Resources/InfraAgent.instructions.md) |
| AppliancesAgent | [AppliancesAgent.instructions.md](Resources/AppliancesAgent.instructions.md) |

## Dependencies

### NuGet packages

| Package | Purpose |
| --- | --- |
| [Azure.Identity](https://www.nuget.org/packages/azure.identity) | Azure authentication |
| [KoenZomers.UniFi.Api](https://www.nuget.org/packages/koenzomers.unifi.api) | Ubiquiti UniFi API client |
| [ModelContextProtocol.AspNetCore](https://www.nuget.org/packages/modelcontextprotocol.aspnetcore) | MCP server middleware for ASP.NET Core |
| [OpenTelemetry](https://www.nuget.org/packages/opentelemetry) | Telemetry SDK |
| [OpenTelemetry.Extensions.Hosting](https://www.nuget.org/packages/opentelemetry.extensions.hosting) | OpenTelemetry host integration |
| [Tiveria.Home.Knx](https://www.nuget.org/packages/tiveria.home.knx) | KNX protocol library |
| [Microsoft.AspNetCore.SignalR.Client](https://www.nuget.org/packages/microsoft.aspnetcore.signalr.client) | SignalR hub client |
| [Microsoft.AspNetCore.SignalR.Client.Core](https://www.nuget.org/packages/microsoft.aspnetcore.signalr.client.core) | SignalR hub client core |
| [Microsoft.AspNetCore.SignalR.Protocols.MessagePack](https://www.nuget.org/packages/microsoft.aspnetcore.signalr.protocols.messagepack) | MessagePack SignalR protocol |
| [Spectre.Console](https://www.nuget.org/packages/spectre.console) | Rich console output |
| [CasCap.Api.Azure.Storage](https://www.nuget.org/packages/cascap.api.azure.storage) | Azure Blob Storage integration |
| [CasCap.Common.Extensions](https://www.nuget.org/packages/cascap.common.extensions) | Shared extension helpers |
| [CasCap.Common.Logging](https://www.nuget.org/packages/cascap.common.logging) | Structured logging helpers |
| [CasCap.Common.Net](https://www.nuget.org/packages/cascap.common.net) | HTTP client helpers |
| [CasCap.Common.Serialization.Json](https://www.nuget.org/packages/cascap.common.serialization.json) | JSON serialisation helpers |
| [CasCap.Common.Caching](https://www.nuget.org/packages/cascap.common.caching) | Caching helpers |
| [CasCap.Common.Services](https://www.nuget.org/packages/cascap.common.services) | Shared service utilities |
| [CasCap.Api.Azure.Auth](https://www.nuget.org/packages/cascap.api.azure.auth) | Azure authentication and token credential helpers |
| [CasCap.Api.Azure.CognitiveServices](https://www.nuget.org/packages/cascap.api.azure.cognitiveservices) | Azure AI Speech synthesis and transcription, used by the `Azure` speech-to-text provider |

### Project references

| Project | Purpose |
| --- | --- |
| `CasCap.Common.AI` | Consolidated MCP tools, prompts, and agent infrastructure |
| `CasCap.Api.SignalCli` | Signal messenger client |
| `CasCap.Api.DDns` | Dynamic DNS service |
| `CasCap.Api.Buderus.Sinks` | Buderus SignalR sink |
| `CasCap.Api.DoorBird.Sinks` | DoorBird SignalR, Redis, Azure Table, and Blob sinks |
| `CasCap.Api.Fronius.Sinks` | Fronius SignalR sink |
| `CasCap.Api.Knx.Sinks` | KNX SignalR sink |
| `CasCap.Api.Miele.Sinks` | Miele SignalR sink |
| `CasCap.Api.EdgeHardware.Sinks` | Edge hardware SignalR sink |
| `CasCap.Api.Shelly.Sinks` | Shelly smart plug SignalR sink |
| `CasCap.Api.Wiz.Sinks` | Wiz smart lighting SignalR sink |
| `CasCap.Api.Ubiquiti.Sinks` | Ubiquiti IP camera SignalR sink |
| `CasCap.Api.Sicce.Sinks` | Sicce SignalR sink |

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
