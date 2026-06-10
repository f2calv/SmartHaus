# SignalCli Library & Test Audit — Remediation Plan

> **Status**: Proposed — findings catalogued, no fixes applied yet. Each item below is to be addressed in turn and its checkbox ticked once complete.

## Overview

This document captures the results of a deep-dive audit of [`CasCap.Api.SignalCli`](../../src/CasCap.Api.SignalCli) and its test project [`CasCap.Api.SignalCli.Tests`](../../src/CasCap.Api.SignalCli.Tests). It catalogues mismatches against the repository `copilot-instructions`, missed performance opportunities, architectural weaknesses, code smells, and documentation drift.

The library wraps the [signal-cli REST API](https://bbernhard.github.io/signal-cli-rest-api/) (generated against API v0.98) behind the `CasCap.Common.Abstractions.INotifier` abstraction, offering two transports:

- **`SignalCliRestClientService`** — HTTP polling, extends `HttpClientBase`.
- **`SignalCliJsonRpcClientService`** — WebSocket push, delegates non-receive operations to the REST client.

The library is largely well-structured (sealed services, thin controller, good XML docs, PII masking via `MaskPhoneNumber()`), but has systematic gaps against the synced conventions. Items are grouped by category and ordered by priority within each group.

## How To Use This Document

Each finding has:

- A **stable ID** (e.g. `RDM-1`) for cross-referencing in commits and PRs.
- A **priority** (`High` / `Medium` / `Low`).
- A checkbox to tick when the fix lands.

Work through the categories top-to-bottom; the highest-impact items are the concurrency bugs (§4), `ConfigureAwait(false)` (§3), README drift (§1), and the test assertion/structure gaps (§7).

---

## 1. Documentation Drift (README ↔ Code)

The synced "README Consistency" rule requires every project README to stay in sync with its implementation. These are clear factual mismatches.

- [x] **`RDM-1` (High)** — `ChannelCapacity` is documented but does not exist. [README.md](../../src/CasCap.Api.SignalCli/README.md) references it in the config table (~line 165), the "Fully configured" JSON example (~line 198), and the class diagram (~line 262), but there is no such property on [`SignalCliConfig`](../../src/CasCap.Api.SignalCli/Models/_SignalCliConfig.cs). Remove the phantom property (or add it if a bounded channel is intended — see `ARC-2`).
- [x] **`RDM-2` (High)** — Transport-mode table is wrong. The README lists only `Rest` and `JsonRpc`, but [`SignalCliTransport`](../../src/CasCap.Api.SignalCli/Models/_Enums.cs) actually has four members: `Normal`, `Native`, `JsonRpc`, `JsonRpcNative`. There is no `Rest` value. Correct the table and the `TransportMode` row.
- [x] **`RDM-3` (Medium)** — DI Registration Flow diagram is incomplete. It shows `INotifier → SignalCliRestClientService` unconditionally and omits the `JsonRpc/JsonRpcNative → SignalCliJsonRpcClientService` branch that [`ServiceCollectionExtensions`](../../src/CasCap.Api.SignalCli/Extensions/ServiceCollectionExtensions.cs) implements.
- [x] **`RDM-4` (Medium)** — Dependency tables are stale. The csproj references `Asp.Versioning.Mvc` and `Microsoft.AspNetCore.Http.Abstractions`, neither of which appears in the README NuGet-packages table.
- [x] **`RDM-5` (Low)** — `BasicAuthEnabled` config property is missing from the README config table.

## 2. Configuration Smells

- [x] **`CFG-1` (High)** — `required` + default initializer is contradictory. In [`SignalCliConfig`](../../src/CasCap.Api.SignalCli/Models/_SignalCliConfig.cs), `HealthCheckUri` and `HealthCheck` are both `[Required] public required ... = <default>;`. The `required` keyword forces the caller to set the value, defeating the default and breaking the "works out-of-the-box" IAppConfig principle. Drop `required` on these two (keep the defaults); `BaseAddress`/`PhoneNumber` correctly stay `required` (no default).
- [x] **`CFG-2` (Low)** — Verify all four `appsettings*.json` tiers (`appsettings.json`, `appsettings.Development.json`, `appsettings.Local.json`, `appsettings.Local.Development.json`) are consistent after any config property rename arising from this audit.

## 3. Missing `ConfigureAwait(false)` (Library Convention)

Both service files are **library** code that never touches `HttpContext`, so per the Performance → ConfigureAwait convention *every* await must use `ConfigureAwait(false)`. Currently **none** of the SignalCli-level awaits do.

- [x] **`AWA-1` (High)** — [`SignalCliRestClientService`](../../src/CasCap.Api.SignalCli/Services/SignalCliRestClientService.cs): add `ConfigureAwait(false)` to every await in the private helpers (`PostBoolAsync`, `PutAsync`, `DeleteAsync`, `DeleteAsync<T>`) and the explicit `INotifier` implementations.
- [x] **`AWA-2` (High)** — [`SignalCliJsonRpcClientService`](../../src/CasCap.Api.SignalCli/Services/SignalCliJsonRpcClientService.cs): add `ConfigureAwait(false)` to `ws.ConnectAsync`, `_webSocket.ReceiveAsync`, `_messageSignal.WaitAsync`, `Task.Delay`, and the WebSocket close in `DisposeAsync`.

## 4. Concurrency / Correctness Bugs (JSON-RPC Client)

The highest-risk findings. The current buffer + counting-semaphore + drain design is fragile.

- [x] **`ARC-1` (High)** — `ConnectAsync` is not thread-safe. It is invoked lazily from `INotifier.ReceiveAsync` when `_webSocket` is null/closed. Two concurrent `ReceiveAsync` callers can both enter, each creating a `ClientWebSocket` *and* a second receive-loop task, leaking a socket and double-buffering. Guard connection establishment with a `SemaphoreSlim`/`Lock`.
- [x] **`ARC-2` (High)** — Semaphore/buffer desync race. After `WaitAsync` + `DrainBuffer()`, the "drain excess semaphore counts" loop can consume a `Release()` whose message was enqueued *after* `DrainBuffer` ran; that message then sits in the buffer with no semaphore count backing it, so the next `ReceiveAsync` blocks despite a buffered message. Replace the manual `ConcurrentQueue` + `SemaphoreSlim` + drain dance with a `Channel<SignalReceivedMessage>` (bounded — this is where a real `ChannelCapacity` config could live, resolving `RDM-1`).
- [x] **`ARC-3` (Medium)** — ~~No startup connection.~~ **Won't fix.** The lazy-connect pattern is correct: `CommunicationsBgService` (the consumer) implements `IBgFeature` and is feature-flag gated. When the comms feature is disabled, the WebSocket must not connect. The first `ReceiveAsync` call — triggered when the feature becomes active — connects automatically via the connect-guard. Adding `IHostedService` would force a connection even when the feature is inactive.

## 5. Performance & Code Smells (Services)

- [x] **`PRF-1` (Medium)** — Exception-driven control flow in `DeserializeMessage`. It deserializes twice and uses two `catch (JsonException)` blocks as the format-detection mechanism. Peek with `JsonDocument`/`Utf8JsonReader` for the `jsonrpc`/`params` keys to avoid throwing on every raw-format frame.
- [x] **`PRF-2` (Low)** — Double `UriBuilder` allocation for logging. `CreateAndConnectWebSocketAsync` builds `wsUri`, then builds a *second* masked URI purely for the log line. Mask the already-built string instead.
- [x] **`PRF-3` (Low)** — `DrainBuffer` allocates twice (a `List<T>` then a collection-expression copy `[.. list]`). Drain straight into the result, or return the list. (Likely removed entirely by `ARC-2`.)
- [x] **`PRF-4` (Low)** — `AddSignalCli` returns `void`. Other `Add*` extension methods conventionally return `IServiceCollection` for chaining.
- [x] **`PRF-5` (Low)** — `ReceiveLoopAsync` uses fully-qualified `System.Text.Encoding.UTF8`. Add `using System.Text;` and shorten to `Encoding.UTF8`.

## 6. Controller

- [x] **`CTL-1` (Medium)** — Null mapped to `Ok` instead of `NotFound`. [`SignalCliController`](../../src/CasCap.Api.SignalCli/Controllers/SignalCliController.cs) wraps nullable service results (`SignalAbout?`, etc.) in `Ok<T>`, producing `200 OK` with a `null` body. Per the "Nullable returns for NotFound patterns" convention, use `Results<Ok<T>, NotFound>` with pattern matching. (Otherwise the controllers are correctly thin pass-throughs with `<inheritdoc cref>` — good.)

## 7. Test Project — Convention Violations

- [x] **`TST-1` (High)** — Many tests have no assertions. Despite names like `..._ReturnsTrue`, methods such as `SetConfiguration_ReturnsTrue`, `SendReaction_ReturnsTrue`, `RemoveReaction_ReturnsTrue`, `SendReceipt_ReturnsTrue`, `RemoteDelete_ReturnsResponse`, `SetPin_ReturnsTrue`, `RemovePin_ReturnsTrue`, `UpdateAccountSettings_ReturnsTrue`, `SetAndRemoveUsername_RoundTrip`, `UpdateContact_ReturnsTrue`, `SyncContacts_ReturnsTrue`, `UpdateProfile_ReturnsTrue`, and `DeleteAttachment_ReturnsFalseForMissingId` only `_output.WriteLine(...)` and never `Assert`. Add meaningful assertions (the names already imply the expected result).
- [x] **`TST-2` (High)** — No `[Trait("Category", "Integration")]` on integration tests. None of the REST tests (all hit a live server) carry the required trait. Add it.
- [x] **`TST-3` (Medium)** — Missing `Tests/Unit/` + `Tests/Integration/` folder structure. Everything sits in the project root, and [`TestBase`](../../src/CasCap.Api.SignalCli.Tests/Tests/Integration/TestBase.cs) is in the root rather than `Tests/Integration/TestBase.cs`. The genuine unit tests (`BuildWebSocketUri`, enum membership, DI registration, JSON deserialization) are mixed with integration tests. Reorganise per convention (preserve git history via `git mv`).
- [x] **`TST-4` (Medium)** — `if (true)` dead conditional in `TestBase.cs` (the HttpClient configure block). Replace with the real `BasicAuthEnabled` check the production registration uses.
- [x] **`TST-5` (Medium)** — Won't fix. `CasCap.Common.Testing` provides `AddXUnitLogging()` infrastructure, not a shared `TestBase` class to inherit. The local TestBase already correctly uses it.
- [x] **`TST-6` (Medium)** — Tests README missing required sections. [Tests/README.md](../../src/CasCap.Api.SignalCli.Tests/README.md) lacks the mandated method-count/test-case-count table, trait-category list, skipped-tests section, and `Tests/` file-structure diagram. It also references a wrong config path (`CasCap:CommsAgentConfig` vs the actual `CasCap:AIConfig:CommsAgent:Settings:GroupName`).
- [x] **`TST-7` (Low)** — No `xunit.runner.json` disabling collection parallelism. These integration tests create/delete groups and set/remove PINs against one real account; concurrent collections can conflict. Add `parallelizeTestCollections: false`.
- [x] **`TST-8` (Low)** — Field naming/readonly inconsistency in `TestBase` — `svc` has no `_` prefix while `_output`/`_config`/`_groupName` do; several fields could be `readonly`.
- [x] **`TST-9` (Low)** — Stray indentation on the `[Fact]` above `CreateUpdateAndDeleteGroup_RoundTrip`.

## 8. Repo-Wide Note (Not SignalCli-Specific)

- [x] **`REPO-1` (Low)** — DTO `record`s are not `sealed`. The Performance convention says entity/DTO types should default to `sealed`, but **no** record in the entire SmartHaus repo is sealed. This is a repo-wide gap; if adopted, do it as a single consistent pass across the repo rather than only in SignalCli.

---

## Suggested Work Batches

To keep PRs focused, address findings in these batches:

| Batch | Findings | Theme |
| --- | --- | --- |
| A | `RDM-1`…`RDM-5`, `CFG-1`, `CFG-2` | README + config sync (factual, low-risk) |
| B | `AWA-1`, `AWA-2`, `PRF-1`…`PRF-5` | `ConfigureAwait(false)` + service smells |
| C | `ARC-1`, `ARC-2`, `ARC-3` | JSON-RPC `Channel<T>` refactor + connect-guard + startup |
| D | `CTL-1` | Controller NotFound mapping |
| E | `TST-1`…`TST-9`, `TST-6` README | Test assertions + restructure + README |
| F | `REPO-1` | Repo-wide `sealed` record pass (separate initiative) |

Batches A, B, D and E are independent and can land in any order. Batch C is the largest and should be reviewed carefully (it subsumes `RDM-1`/`PRF-3`).
