---
name: sync-appsettings-to-configmap
description: 'Synchronize an appsettings JSON file into an existing ConfigMap in a caller-configured private GitOps repository. Use after local deployment configuration changes, before an application rollout, or when checking configuration drift.'
argument-hint: '[mode={check|sync}]'
user-invocable: true
---

# Sync Appsettings To ConfigMap

Use the central **`sync-file-to-configmap`** skill for all generic synchronization mechanics,
privacy review, dry-run, verification and GitOps approval steps. Load that skill before proceeding;
stop if it is not available. This wrapper supplies only the application-specific source and mapping.

## Application Inputs

- Source: the caller-selected application-local appsettings file contained by this application
  checkout; typically `appsettings.Local.json`.
- Format: `Json` (including JSON comments and trailing commas); embed the entire reviewed text.
- The application's secret provider remains authoritative for credentials. A local appsettings
  filename does not establish that its contents are suitable for a ConfigMap.
- Destination: preserve the existing caller-configured private GitOps target. Load its coordinates
  from the gitignored `deploy.local.psd1`, never from this skill's installation location.

Map the existing local settings to central inputs without changing their stored values:

| Existing caller input | Central input |
| --- | --- |
| Application checkout / `ApplicationRoot` | `SourceRoot` |
| Selected `SourcePath` | `SourcePath` |
| `ManifestRepo` | `TargetRoot` |
| `AppSettingsConfigMapPath` | `TargetPath` |
| `AppSettingsConfigMapName` | `ConfigMapName` |
| `AppSettingsConfigMapKey` (normally `appsettings.Local.json`) | `DataKey` |
| Explicitly verified existing namespace | `ConfigMapNamespace` |

The central script does not accept the old deployment-data schema directly. Either pass mapped
arguments in memory or create a separately gitignored local data file using the central template.
Keep the existing `deploy.local.psd1` and its consumers unchanged. Resolve and confirm the existing
namespace locally without dumping the manifest or its embedded content.

## Caller Migration Boundary

The central skill is the workflow for new manual synchronization. Existing bundled scripts and
deployment callers are **not migrated** by this documentation change.

TODO: Migrate `scripts/Sync-AppSettingsToConfigMap.ps1` and its deployment callsites to the central
mechanics after validating parameter precedence, namespace mapping, explicit non-secret review,
dry-run, exact normalized-text equality and rollback with synthetic caller tests. The existing
implementation creates a plaintext temporary source copy and therefore does not meet the central
privacy boundary; do not use it for a new manual synchronization. It remains in place only to avoid
silently breaking unvalidated deployment callers. Remove the duplicated implementation once those
callers are validated, rather than maintaining two synchronization policies.

## Maintaining This Skill

Recheck this mapping when the appsettings tiers, deployment-data keys, secret provider or deployment
callers change. Generic behavior and test guidance belong in `sync-file-to-configmap`.
