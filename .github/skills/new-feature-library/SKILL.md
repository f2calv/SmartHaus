---
name: new-feature-library
description: 'Create a feature integration and optional sinks library for the KNX smart home application. Use for the application-specific event/sink contract, lite-mode registration, feature-gated MCP and controller wiring; central .NET skills and instructions own coding conventions.'
disable-model-invocation: true
---

# New Feature Library

Add a device integration to this application's existing event/sink architecture. Read the central
C#, configuration, Azure Table, MCP, Web API, NuGet and testing instructions before editing their
matching files. Load `dotnet-apphost`, `dotnet-dependency-injection`, `dotnet-mcp` and
`kubernetes-helm` for the surfaces being changed, plus the relevant HTTP, Redis or background
pipeline skill. Stop if the shared customizations are unavailable.

Do not copy those conventions or a neighbouring feature wholesale. Inspect its actual contracts
and current host composition; an existing implementation is evidence of integration behavior,
not authority to reproduce naming or lifecycle drift.

## 1. Establish The Integration Contract

Obtain the feature name, MCP domain, source protocol, event fields/types/units, stable device
identity, single/multi-device storage model and alert conditions. Decide which sinks and APIs are
needed before creating projects.

- Base library: `src/CasCap.Api.{Feature}/`.
- Optional persistence sinks library: `src/CasCap.Api.{Feature}.Sinks/`. Do not add a separate
  project or storage dependency when the integration does not need it.
- Test coverage follows the existing feature and backend test projects.
- Redis, table names and sink settings must be chosen from the shared sink/key conventions;
  configuration and fixtures use public-safe placeholders, never operational endpoints.

Inspect an existing feature's base and extra-sink registration, query service, sink query contract,
controller and host registration. Reuse shared event-sink and configuration helpers.

## 2. Implement The Base Feature

1. Follow the repository's current target frameworks and conditional dependency pattern rather
   than hardcoding an SDK or framework from an old scaffold. Add solution entries as described
   below; keep persistence dependencies out of the base library.
2. Define the feature event with stable device identity and a UTC observation time. Define
   snapshots only where consumers need a distinct current-state projection. Map protocol DTOs
   at the client boundary.
3. Define `{Feature}Config` under `CasCap:{Feature}Config`, with source settings, polling controls,
   sink selection and any alert thresholds. Do not duplicate sink configuration owned by shared
   types.
4. Implement the protocol client and monitor. Fan observations out through
   `IEventSink<{Feature}Event>`; use the existing `IBgFeature` host contract for a feature worker.
5. Add only the lightweight Console, Memory or Metrics sinks needed by this integration. The
   memory sink is the fallback query-capable primary when no configured sink supplies one.
6. Define a capability-named query abstraction for interchangeable sinks, such as
   `I{Feature}Query`. Keep the application query service separate from the sink contract.
   Introduce another interface only for a demonstrated boundary or test substitution, with a
   distinct capability name such as `I{Feature}Readings`; do not scaffold
   `I{Feature}QueryService` or rename unrelated existing interfaces in this task.
7. Let the query service delegate to the selected primary query sink. Expose snapshots and
   bounded event/history queries through the feature controller; include direct device commands
   only when explicitly required.
8. Provide a connection health check when the source has a meaningful non-destructive probe.

## 3. Register Base And Extra Sinks

`Add{Feature}(configuration, lite, configure)` owns configuration, clients, base sinks and queries.
It must be safe to register through both the feature and Comms paths without duplicating clients,
sink instances or workers.

- Select the primary query sink through the existing sink-discovery/keyed-registration helpers.
  If none qualifies, register one memory fallback and alias its interfaces to that same instance.
- `lite: true` retains query dependencies but does not start the monitor or feature health checks.
- Register the full worker and configured probe only when the feature itself is enabled.

When persistent sinks are required, `Add{Feature}WithExtraSinks(...)` calls the base registration
and discovers sinks from its assembly plus any supplied `additionalSinkAssemblies`. Preserve the
current lite-mode exclusions of the chosen integration; do not copy another feature's exclusion
list without checking its query/storage behavior.

- Azure Tables: map reading and snapshot entities using the central entity/key/versioning rules.
  Read table names through `SinkSettingKeys` and configured sink settings.
- Redis: preserve the snapshot/history contract through the shared caching layer. Do not limit a
  requested history range to today's bucket unless that is the explicit API contract.
- CommsStream: implement the agreed per-device transition or hysteresis/cooldown behavior.
  Suppress first-seen transition alerts; select the existing clock and state helpers.
- Application-specific sinks belong in the backend, not in reusable provider libraries.

## 4. Wire The Host And MCP

Inspect the current composition helpers, not an old `Program.cs` snippet:

1. Add the feature constant in `src/CasCap.Backend/Constants/FeatureNames.cs`.
2. Register the feature in `src/CasCap.App.Server/AppHost.Features.cs`, supplying the backend
   assembly when it owns additional sinks. Add Comms to its activation condition only if Comms
   needs this feature's queries, using lite mode when the feature worker is disabled.
3. Add `{Domain}McpQueryService` and optional prompts in the backend MCP surface. Delegate to the
   application query capability; use current tool authorization and approval conventions.
4. Add a feature-gated entry in
   `src/CasCap.Backend/Extensions/HausMcpServiceCollectionExtensions.cs`. Register exact tool
   and prompt types; do not introduce assembly-wide tool discovery or a parallel MCP server.
5. Add controller discovery through `src/CasCap.App.Server/AppHost.WebApi.cs`, gated by the feature.
6. Keep the console host consistent only if it supports the new feature. Do not make a disabled
   feature start because another host happens to register its query facade.

## 5. Synchronize Configuration And Packaging

- Update all existing appsettings tiers under the central configuration synchronization rule;
  private local files contain only their overrides and are never copied into tracked examples.
- Add the feature to `EnabledFeatures` only in the tiers where it should run. Comms query access
  alone must not enable the monitor.
- Add the created projects to both `SmartHaus.Debug.slnx` and `SmartHaus.Release.slnx` in the
  existing library groups, preserving Debug project and Release package resolution.
- Update the base/sinks READMEs with the actual contracts and configuration.
- Update `charts/smarthaus/values.yaml` only when the feature belongs in public deployment defaults.
  Keep environment-specific configuration and operational deployment procedures out of this repo.

## 6. Verify

Build the affected projects/solution. With test approval, cover event mapping, primary/fallback
sink selection, repeated registration, full/lite behavior, selected persistence queries and alerts.
Verify that disabled features expose neither controllers nor MCP tools, that enabled/Comms tool
sets resolve, and that lite registration starts no feature worker. Validate affected configuration
and chart contracts through their existing checks.

## Maintaining This Skill

- Recheck feature/sink contracts, primary selection and lite-mode exclusions when shared packages change.
- Update host/helper paths and exact-type MCP registration when composition is refactored.
- Keep framework, coding, storage, documentation and testing rules in their central owners rather
  than freezing another copy of them in this checklist.
