# SignalCli Receive Liveness — Active Heartbeat Design

> **Status**: **Blocked / parked (2026-06-14).** Phase 0 (`HB-0`) was prototyped and tested against a live linked device, and it **invalidated the core premise of this design**: a self-addressed action does **not** echo back to the originating `signal-cli` device, so it cannot be used as a liveness probe. All prototype code was reverted. The passive watchdog (`ReceiveStalenessTimeoutMs`) remains the only shipped liveness mechanism. See [Phase 0 Findings](#phase-0-findings-hb-0--2026-06-14--premise-invalidated) below before resuming this work. The original proposal is retained unchanged for historical context.

## Overview

The [`CasCap.Api.SignalCli`](../../src/CasCap.Api.SignalCli) JSON-RPC transport ([`SignalCliJsonRpcClientService`](../../src/CasCap.Api.SignalCli/Services/SignalCliJsonRpcClientService.cs)) holds a long-lived WebSocket to the signal-cli REST API, which in turn bridges to the Java `signal-cli` daemon. A known failure mode (see [001](001-signalcli-audit-remediation.md) and the upstream issue drafts) leaves **outbound sending healthy while inbound delivery silently stops** — e.g. a poisoned `msg-cache` envelope kills only the daemon's receive thread. The WebSocket stays open, the process stays alive, and nothing detects the outage.

A **passive** watchdog (`ReceiveStalenessTimeoutMs`) was added as a cheap backstop: it forces a reconnect when no inbound frame arrives within a timeout. Its fatal limitation is that it cannot distinguish *"the receive path is dead"* from *"nobody has messaged this account."* For a low-traffic account (SmartHaus is queried every 3–5 days) the timeout would have to exceed the longest legitimate quiet period (~7 days), making detection so slow it is nearly worthless. An account with no inbound integration at all has no organic traffic, so the passive watchdog is permanently disabled there.

An **active heartbeat** removes this ambiguity by *generating* inbound traffic on a schedule and verifying it round-trips, giving minutes-level detection latency regardless of organic traffic — and working identically for quiet accounts and zero-inbound accounts.

> ⚠️ **This premise was tested and proven false — see [Phase 0 Findings](#phase-0-findings-hb-0--2026-06-14--premise-invalidated).** A self-addressed action never returns to the device that sent it, so the active-heartbeat approach as designed below cannot work. The remainder of this document is preserved as the original proposal for historical context.

## Phase 0 Findings (`HB-0`) — 2026-06-14 — Premise Invalidated

A Development-only diagnostic probe was built to validate `HB-0`: an endpoint that sends a self-addressed action (typing indicator **or** read receipt) and waits up to 5 s for **any** inbound frame, while also reporting the receive WebSocket's connection state and the time since the last inbound frame. It was exercised against the live linked `signal-cli` device.

**Result — no self-action echoes back to the originating device:**

| Probe | Send result | Receive WebSocket | ms since last inbound frame | Echo within 5 s |
| --- | --- | --- | --- | --- |
| Read receipt → self | `SUCCESS` | `Open` (connected) | 27,658 | ❌ none |
| Typing indicator → self | `SUCCESS` | `Open` (connected) | 43,533 | ❌ none |
| Read receipt → self | `SUCCESS` | `Open` (connected) | 52,492 | ❌ none |

The "ms since last inbound frame" counter climbed monotonically (≈27 s → 43 s → 52 s) across the three probes, proving **no inbound frame of any kind** (echo or organic) arrived for the entire ~52 s window — the WebSocket was confirmed `Open` throughout, so this was not a connectivity artifact. The `signal-cli` debug log corroborates this exactly: every `sendReceipt` / `sendTyping` returned `type: SUCCESS`, but **no subsequent inbound `json-rpc received data` frame followed** — only `Received pong` keep-alives.

**Root cause.** `signal-cli` is a **linked** device. When a linked device sends a syncable action, the Signal server fans the sync transcript out to the account's **other** linked devices — **never back to the originating device**. Therefore an account pinging *itself* can never observe its own sync transcript; the transcript is delivered to the primary (Android) device instead. The design's central assumption — ["Why a self-action loops back as inbound"](#why-a-self-action-loops-back-as-inbound) — is incorrect.

> **Note on the earlier apparent success.** A single ~2 s "receipt echo" observed in an earlier ad-hoc test was a **false positive**: it was coincidental organic inbound traffic (most likely a receipt/message arriving *from* the Android primary while the Signal app was open), not the self-receipt syncing back. The self-diagnosing probe (which reports WebSocket state and inter-frame timing) is what made this distinction visible.

**Implication.** The active heartbeat as specified below cannot detect a dead receive thread, because there is no non-visible self-action that returns to `signal-cli`. Options for a future attempt:

1. **Note-to-Self real message** — the only action likely to round-trip to the originating device is an actual message to the account's own number. This *is* visible in the Note-to-Self thread (could be mitigated with a disappearing-message timer / auto-delete) and was explicitly rejected by the [No Visible Messages constraint](#the-critical-constraint-no-visible-messages). Untested — it is unconfirmed whether even a Note-to-Self message echoes to the sending linked device.
2. **Passive watchdog only** — accept that a genuinely silent account cannot be distinguished from a dead receive thread, and rely solely on `ReceiveStalenessTimeoutMs`. This is the current shipped state.
3. **Second-device / out-of-band trigger** — have a *different* device or service send to the account on a schedule. Out of scope here.

**Decision: parked.** All `HB-0` prototype code (probe endpoint, probe DTO, heartbeat loop, health check, config properties, enum) was reverted on 2026-06-14. To be revisited another time.

## The Critical Constraint: No Visible Messages

> **Will my Android Signal app show a "hello world" ping every hour? — No. It must not, and it does not have to.**

This is the dealbreaker requirement: a heartbeat that posts a visible message to **Note to Self** (or any chat) every N minutes is unacceptable. Fortunately, Signal's protocol gives us several inbound-generating actions that produce **no chat artifact** on the primary (Android) device:

> ⚠️ The "Generates inbound frame?" column below reflects the **original (incorrect) assumption**. Phase 0 testing proved that self-addressed typing indicators and receipts do **not** generate an inbound frame on the originating device (see [Findings](#phase-0-findings-hb-0--2026-06-14--premise-invalidated)). The table is retained as originally written.

| Mechanism | Visible on Android? | Generates inbound frame? (assumed) | Notes |
| --- | --- | --- | --- |
| Message to *Note to Self* | **Yes** (chat entry) | Yes (sync transcript) | ❌ Unacceptable — rejected. The only candidate that might actually round-trip to the sender (untested). |
| **Typing indicator to self** | No | ~~Yes (sync transcript)~~ **No — disproven** | Ephemeral; never stored. Phase 0: sent `SUCCESS`, no echo. |
| **Receipt to self** ([`SendReceipt`](../../src/CasCap.Api.SignalCli/Services/SignalCliRestClientService.cs)) | No | ~~Yes (sync transcript)~~ **No — disproven** | Phase 0: sent `SUCCESS`, no echo. |
| **Reaction add+remove** ([`SendReaction`](../../src/CasCap.Api.SignalCli/Services/SignalCliRestClientService.cs) / `RemoveReaction`) | Briefly | Yes (sync transcript) | Visible flicker — avoid. Untested; same self-sync limitation likely applies. |

### Why a self-action loops back as inbound

> ⚠️ **Disproven by Phase 0 testing — see [Findings](#phase-0-findings-hb-0--2026-06-14--premise-invalidated).** The reasoning below is wrong: the sync transcript is fanned out to the account's *other* linked devices, **not** back to the device that originated the action. A self-action therefore never returns to `signal-cli`.

`signal-cli` is a **linked device**, not the primary. When *any* linked device sends *anything* (even an ephemeral typing indicator), the Signal server fans out a **sync transcript message** to all of the account's other linked devices — including `signal-cli` itself. That transcript arrives on the **same WebSocket receive stream** we are trying to prove is alive. So the heartbeat does not require a second account or a real recipient: signal-cli pinging *itself* via a non-visible action is sufficient to exercise the full inbound pipeline.

The recommended primitive is a **typing indicator addressed to our own number** (or to a self-owned group), because it is the only option that is guaranteed ephemeral end-to-end — never persisted, never rendered, never notified.

> **Open verification item**: confirm signal-cli/REST-API exposes a `sendTyping`/typing endpoint and that the resulting sync transcript is delivered back over `/v1/receive`. If typing indicators are not surfaced on the receive stream, fall back to a self-`SendReceipt`, which the codebase already supports. This must be validated against a live linked device before implementation (see Phase 0).

## Reasons to Build It (and Reasons Not To)

**For:**

- Detects the silent-inbound-failure symptom in minutes, not days — the only fault we currently cannot observe.
- Account-traffic-agnostic: works for quiet accounts and zero-inbound accounts alike.
- Reuses existing send primitives and the existing reconnect machinery.

**Against / cost:**

- Adds periodic outbound traffic and a self-message loop — small but non-zero load on the REST API and daemon.
- Requires careful non-visibility verification (Phase 0) to avoid the unacceptable Android-notification outcome.
- The passive watchdog already covers high-traffic accounts; the heartbeat only earns its keep on low/zero-traffic accounts.

**Recommendation:** Build it, but gate it behind config that is **off by default**, and ship Phase 0 (non-visibility proof) before any production rollout.

## Reusing Existing Health-Check Infrastructure

The heartbeat should mirror the established **staleness-health-check** pattern rather than inventing a new observability surface.

### 1. The staleness-health-check pattern — the closest precedent

The ideal shape is a stream-staleness health check that reports `Healthy`/`Degraded`/`Unhealthy` based on **how long since the last inbound item**, with severity modulated by whether traffic is *expected* right now. The SignalCli analogue:

- "last item received" timestamp → `_lastFrameTicks` (already stamped by the passive watchdog).
- "no traffic expected right now" → "heartbeat disabled / account legitimately idle."
- Severity tiers map cleanly: frames flowing → `Healthy`; heartbeat overdue but reconnect in progress → `Degraded`; heartbeat round-trip failed N times → `Unhealthy`.

This means the heartbeat's *result* should be surfaced as an `IHealthCheck` (e.g. `SignalCliReceiveHeartbeatHealthCheck`) using `TimeProvider`-driven elapsed-time logic, so it plugs into the existing `/healthz` endpoints and alerting with zero new plumbing.

### 2. `KubernetesProbeTypes` enum — config wiring

[`KubernetesProbeTypes`](../../../CasCap.Common/src/CasCap.Common.Abstractions/_Enums.cs) (`None`/`Readiness`/`Liveness`/`Startup`, `[Flags]`) plus the [`GetTags()`](../../../CasCap.Common/src/CasCap.Common.Extensions.Diagnostics.HealthChecks/Extensions/KubernetesExtensions.cs) extension is the established pattern every SmartHaus feature uses to register a health check (see [`BuderusServiceCollectionExtensions`](../../src/CasCap.Api.Buderus/Extensions/ServiceCollectionExtensions.cs)). The heartbeat health check registers identically:

```csharp
if (config.HeartbeatHealthCheck != KubernetesProbeTypes.None)
    services.AddHealthChecks()
        .AddCheck<SignalCliReceiveHeartbeatHealthCheck>(
            SignalCliReceiveHeartbeatHealthCheck.Name,
            tags: config.HeartbeatHealthCheck.GetTags());
```

A dead receive thread is a **liveness** failure (the pod should be restarted), so the recommended default tag once enabled is `Liveness` — but it stays `None` (disabled) until Phase 0 validation passes.

### 3. `SignalCliConnectionHealthCheck` / `HttpEndpointCheckBase` — not a fit

The existing [`SignalCliConnectionHealthCheck`](../../src/CasCap.Api.SignalCli/HealthChecks/SignalCliConnectionHealthCheck.cs) only probes the REST API's `/v1/about` endpoint via [`HttpEndpointCheckBase`](../../../CasCap.Common/src/CasCap.Common.Extensions.Diagnostics.HealthChecks/Diagnostics/HealthChecks/HttpEndpointCheckBase.cs). That proves the **Go REST API** is reachable — it says nothing about the **Java daemon's receive thread**, which is precisely the layer that fails silently. The heartbeat is complementary, not a replacement.

## Proposed Implementation

### Components

```mermaid
flowchart LR
    Timer[Heartbeat timer\nHeartbeatIntervalMs] -->|self typing indicator| REST[signal-cli REST API]
    REST --> Daemon[Java signal-cli daemon]
    Daemon -->|sync transcript| WS[WebSocket receive loop]
    WS -->|stamp _lastHeartbeatSeenTicks| State[(Liveness state)]
    Timer -->|expects echo within HeartbeatTimeoutMs| State
    State -->|overdue x N| HC[SignalCliReceiveHeartbeatHealthCheck]
    State -->|overdue| Reconnect[Abort WebSocket -> reconnect]
```

1. **Heartbeat sender** — a `PeriodicTimer` loop (mirroring the existing watchdog loop in `SignalCliJsonRpcClientService`) sends a self-addressed **typing indicator** every `HeartbeatIntervalMs`, tagging each ping with the send timestamp.
2. **Echo detector** — the receive loop already deserializes every inbound frame; extend it to recognise the self-sync transcript and stamp `_lastHeartbeatSeenTicks`. (If the transcript can't be correlated precisely, treat *any* inbound frame after a ping as proof of life — the goal is liveness, not exactly-once accounting.)
3. **Failure action** — if a ping is not echoed within `HeartbeatTimeoutMs`, log `LogError`, increment a consecutive-miss counter, and `Abort()` the WebSocket to force the existing reconnect path. A reconnect alone will **not** clear a poisoned `msg-cache`; the loud error is the actionable signal, and the health check escalates to `Unhealthy` after `HeartbeatFailureThreshold` consecutive misses so Kubernetes restarts the pod.
4. **Health check** — `SignalCliReceiveHeartbeatHealthCheck` reports status from the consecutive-miss counter and `_lastHeartbeatSeenTicks` elapsed time, using `TimeProvider` per the staleness-health-check precedent.

### Configuration (additions to `SignalCliConfig`)

| Property | Type | Default | Purpose |
| --- | --- | --- | --- |
| `HeartbeatIntervalMs` | `int` | `0` (disabled) | How often to send the self-ping. Suggested production value ~30 min. `0` disables the heartbeat entirely. |
| `HeartbeatTimeoutMs` | `int` | `300000` | Max wait for a ping to echo back before counting a miss (~5 min). |
| `HeartbeatFailureThreshold` | `int` | `3` | Consecutive missed echoes before the health check reports `Unhealthy`. |
| `HeartbeatHealthCheck` | `KubernetesProbeTypes` | `None` | Probe tags for the heartbeat health check; `None` until Phase 0 passes, then `Liveness`. |

All four follow the existing `IAppConfig` conventions (validation attributes, `<see cref>` deep links to the consuming service) and must be synced across the five config layers (`appsettings.json`, `appsettings.Development.json`, the gitignored Local tiers, and the prod ConfigMap [`haus-appsettings.yaml`](../../../KNX_K8S/src/workloads/configmaps/prd-k3s/haus-appsettings.yaml)). The existing `ReceiveStalenessTimeoutMs` passive watchdog remains and can coexist (belt-and-braces for high-traffic accounts).

### Recommended Settings by Account

| Account | `ReceiveStalenessTimeoutMs` | `HeartbeatIntervalMs` | Rationale |
| --- | --- | --- | --- |
| **SmartHaus** | `0` (passive watchdog useless at 3–5 day cadence) | ~`1800000` (30 min) once Phase 0 passes | Active heartbeat is the only viable detector for a quiet account. |
| **Zero-inbound account** | `0` | `0` for now (no inbound integration) → enable if/when inbound is added | Nothing receives inbound yet; revisit when integration lands. |
| **High-traffic account** (hypothetical) | non-zero (e.g. a few hours) | `0` | Organic traffic makes the cheap passive watchdog sufficient. |

## Phased Plan

- [x] **`HB-0` (Blocking)** — *Non-visibility proof.* **DONE — FAILED (2026-06-14).** Non-visibility held (nothing appeared on Android), but condition (b) **failed**: self typing indicators and self receipts do **not** produce an inbound frame on `/v1/receive`, because a linked device's sync transcript is never delivered back to itself. This invalidates the whole approach — see [Phase 0 Findings](#phase-0-findings-hb-0--2026-06-14--premise-invalidated). **Blocked: do not proceed with HB-1…HB-5 as designed.**
- [ ] **`HB-1`** — Add the four config properties to `SignalCliConfig` + sync all config layers + README.
- [ ] **`HB-2`** — Implement the heartbeat sender loop and echo detection in `SignalCliJsonRpcClientService`, reusing the existing `PeriodicTimer`/abort-reconnect pattern.
- [ ] **`HB-3`** — Implement `SignalCliReceiveHeartbeatHealthCheck` (model on the staleness-health-check pattern) and wire it via `KubernetesProbeTypes`/`GetTags()`.
- [ ] **`HB-4`** — Unit tests (fake `TimeProvider`, simulated missed/late/on-time echoes) + a manually-run integration test against the live demo daemon.
- [ ] **`HB-5`** — Enable on SmartHaus (`HeartbeatIntervalMs` ~30 min, `HeartbeatHealthCheck = Liveness`); leave zero-inbound accounts disabled.

## Open Questions

1. ~~Does signal-cli's REST API expose typing indicators, and do they round-trip on `/v1/receive`?~~ **Answered (2026-06-14):** typing indicators *and* receipts are exposed and send `SUCCESS`, but **neither round-trips to the originating device** — a linked device never receives its own sync transcript. Neither is usable as a heartbeat. Open follow-up: does an actual *Note-to-Self message* round-trip to the sending linked device? (Untested; only pursue if the visibility constraint can be relaxed.)
2. Can we correlate the echoed sync transcript back to a specific ping timestamp, or do we accept "any inbound after a ping = alive"? The latter is simpler and sufficient for liveness.
3. Should a sustained heartbeat failure (poisoned cache that survives reconnects) escalate beyond pod restart — e.g. an out-of-band alert via a *different* notification channel, since the Signal path itself is the thing that's broken?
