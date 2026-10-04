# CasCap.Api.Fronius

A .NET library that integrates with a [Fronius](https://www.fronius.com) solar inverter (Symo Gen24) via its local Solar API v1, samples power-flow data every second, and dispatches each reading to configurable sinks.

## Installation

```bash
dotnet add package CasCap.Api.Fronius
```

## Purpose

The library is built around one background service that forms the core pipeline:

**`FroniusMonitorBgService`** – Waits for the device health check to pass, then polls `GetPowerFlowRealtimeData` every second. Each response is wrapped in a `FroniusEvent` containing the five key power metrics (SOC, P_Akku, P_Grid, P_Load, P_PV) and a UTC timestamp. OpenTelemetry gauges are recorded for each metric before the event is dispatched in parallel to every registered `IEventSink<FroniusEvent>` implementation.

A REST API (`FroniusController`) exposes endpoints for real-time power flow, inverter data, meter readings, storage state, and historical line items.

### Sinks

| Sink | Description |
| --- | --- |
| **Console** | Logs every event via the .NET logger (Debug level) |
| **Memory** | Tracks the latest power-flow reading in memory for snapshot queries |
| **Metrics** | Emits power and percentage gauges via OpenTelemetry metrics |

Optional Redis and Azure Tables implementations are provided by
[`CasCap.Api.Fronius.Sinks`](../CasCap.Api.Fronius.Sinks).

## Event Flow

```mermaid
flowchart TD
    INVERTER["Fronius Inverter\n(LAN REST API v1)"]

    subgraph Monitor["FroniusMonitorBgService (every 1 s)"]
        HEALTH["Await connection\nhealth check"]
        FETCH["GetPowerFlowRealtimeData"]
        BUILD["Build FroniusEvent\n(SOC, P_Akku, P_Grid, P_Load, P_PV, UtcDateTime)"]
        METRICS["Record OpenTelemetry gauges"]
        DISPATCH["Fan-out to all sinks\nTask.WhenAll"]
    end

    SINK_CONSOLE["Console Sink\n(logger)"]
    SINK_MEMORY["Memory Sink\n(latest power-flow reading)"]
    SINK_METRICS["Metrics Sink\n(power gauges)"]

    CLIENT["FroniusClientService\n(powerflow, inverter, meter, storage, …)"]

    INVERTER -->|HTTP REST| CLIENT
    CLIENT --> FETCH
    HEALTH --> FETCH --> BUILD --> METRICS --> DISPATCH
    DISPATCH --> SINK_CONSOLE
    DISPATCH --> SINK_MEMORY
    DISPATCH --> SINK_METRICS
```

## Configuration Examples

### Minimal

```json
{
  "CasCap": {
    "FroniusConfig": {
      "BaseAddress": "http://192.168.1.248",
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
    "FroniusConfig": {
      "BaseAddress": "http://192.168.1.248",
      "HealthCheckUri": "solar_api/v1/GetPowerFlowRealtimeData.fcgi",
      "HealthCheck": "Readiness",
      "PollingIntervalMs": 1000,
      "ConnectionPollingDelayMs": 1000,
      "ConnectionLogEscalationInterval": 10,
      "SocAlertThreshold": 0.95,
      "SocAlertHysteresis": 0.05,
      "SocAlertCooldownMs": 300000,
      "Sinks": {
        "AvailableSinks": {
          "Console": { "Enabled": true },
          "Memory": { "Enabled": true },
          "Metrics": { "Enabled": true }
        }
      }
    }
  }
}
```

## License

This project is released under [The Unlicense](../../LICENSE). See the [LICENSE](../../LICENSE) file for details.
