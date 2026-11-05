# Architecture

These source-derived views complement the [System Context](../README.md#system-context) and
the [backend component view](../src/CasCap.Backend/README.md#components). Workloads select
features from one application image; the [chart dependency graph](../charts/smarthaus/README.md#dependency-graph)
shows the separately configurable process aliases.

## Data Flow

Device features dispatch readings and webhook events to selected sinks. Redis connects event and
media producers to communications; the external runtime owns model execution and calls domain MCP tools.

```mermaid
flowchart LR
    classDef owned fill:#dbeafe,stroke:#1d4ed8,color:#1e3a8a
    classDef external fill:#fef3c7,stroke:#d97706,color:#78350f
    classDef store fill:#e5e7eb,stroke:#4b5563,color:#111827
    devices["Devices and cloud APIs"]:::external --> features["Feature services"]:::owned
    features --> sinks["Event sinks"]:::owned
    sinks --> redis[("Redis state, streams<br/>and media cache")]:::store
    sinks -.-> storage[("Optional Azure Storage")]:::external
    sinks --> hub["SignalR hub"]:::owned
    redis --> comms["Communications and media"]:::owned
    comms --> runtime["Agent Runtime"]:::external
    runtime --> mcp["Domain MCP tools"]:::owned
    mcp --> features
    signal["Signal gateway"]:::external <-->|"messages and attachments"| comms
```

## Dependency Graph

Release builds use verified Common 5.1.0, Azure 2.0.0, Signalizr 1.2.0 and Agent Runtime 1.4.0
packages. Debug uses adjacent source projects for those dependencies. Same-repository references
remain project references in both configurations.

```mermaid
graph TD
    classDef owned fill:#dbeafe,stroke:#1d4ed8,color:#1e3a8a
    classDef published fill:#dcfce7,stroke:#15803d,color:#14532d
    classDef external fill:#fef3c7,stroke:#d97706,color:#78350f
    server["HTTP server"]:::owned --> hosting["Shared application hosting"]:::owned
    console["Console host"]:::owned --> hosting
    hosting --> backend["Backend"]:::owned
    backend --> sinks["Integration sink libraries"]:::published
    sinks --> clients["Device client libraries"]:::published
    backend --> ddns["Dynamic DNS"]:::owned
    backend --> comms["Signalizr client and Comms.AI<br/>1.2.0"]:::external
    backend --> runtime["Agent Runtime contracts<br/>1.4.0"]:::external
    backend & sinks & ddns --> azure["Azure packages<br/>2.0.0"]:::external
    hosting & backend & clients & sinks & ddns --> common["Common packages<br/>5.1.0"]:::external
```

The client/sink families include four published pairs: Buderus, DoorBird, Fronius and KNX.
Other integration families remain source-only, as listed in the [project catalogue](../README.md#projects).
Green nodes identify families containing published packages, not every integration's publication status.

## Workflow

The [publication flow](artifact-publication.md#deployment-flow) follows the actual CI and manual
artifact workflows. Neither publication nor a release tag establishes a live deployment.
