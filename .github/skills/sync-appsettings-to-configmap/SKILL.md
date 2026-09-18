---
name: sync-appsettings-to-configmap
description: 'Synchronize an appsettings JSON file into an existing ConfigMap in a caller-configured private GitOps repository. Use after local deployment configuration changes, before an application rollout, or when checking configuration drift. - Brought to you by f2calv/SmartHaus'
argument-hint: '[mode={check|sync}]'
user-invocable: true
---

# Sync Appsettings To ConfigMap

Synchronize an application-local JSON configuration file into an existing ConfigMap tracked by a
private GitOps repository. The bundled script updates one configured `data` key, preserves the
surrounding manifest, and never commits, pushes, or mutates the cluster.

## Prerequisites

- PowerShell 7.4 or later
- Mike Farah `yq` available on `PATH`
- The application and private GitOps repositories available locally
- A parseable appsettings JSON file; JSON comments and trailing commas are supported
- A clean target ConfigMap path in the GitOps worktree
- A gitignored `deploy.local.psd1` containing the GitOps repository, target path and ConfigMap name

## Quick Start

Check the source and target without changing the GitOps worktree:

```powershell
# Run from the application repository root.
./.github/skills/sync-appsettings-to-configmap/scripts/Sync-AppSettingsToConfigMap.ps1 -WhatIf
```

Synchronize the ConfigMap:

```powershell
# Run from the application repository root.
./.github/skills/sync-appsettings-to-configmap/scripts/Sync-AppSettingsToConfigMap.ps1
```

## Parameters

| Parameter | Meaning |
| --- | --- |
| `-SourcePath` | Appsettings source contained by the application repository |
| `-ApplicationRoot` | Application repository root; defaults to the current Git worktree |
| `-DeployConfigPath` | Gitignored deployment data; defaults to `deploy.local.psd1` |
| `-ManifestRepo` | Optional explicit GitOps checkout path overriding local deployment data |
| `-AppSettingsConfigMapPath` | Optional GitOps-relative ConfigMap path overriding local deployment data |
| `-AppSettingsConfigMapName` | Optional expected ConfigMap name overriding local deployment data |
| `-AppSettingsConfigMapKey` | ConfigMap data key; defaults to `appsettings.Local.json` |
| `-WhatIf` | Validate inputs and describe the write without changing the target |

## Local Configuration

Keep real coordinates only in the gitignored `deploy.local.psd1`. Start from
`deploy.local.psd1.example` and provide:

```powershell
@{
    ManifestRepo             = 'C:\path\to\gitops-repository'
    AppSettingsConfigMapPath = 'path/to/appsettings-configmap.yaml'
   AppSettingsConfigMapName = 'application-settings'
   AppSettingsConfigMapKey  = 'appsettings.Local.json'
}
```

Explicit command-line values override local deployment data. Never add real deployment coordinates to
tracked examples, documentation, issues, pull requests, or commit messages.

The script does not derive application state from its installation path. When invoked from a
centralized skill, run it with the application repository as the current directory or pass explicit
`-ApplicationRoot` and `-SourcePath` values. Source and target containment is enforced independently.

## Required Protocol

1. Resolve the configured source inside the application repository. Never synchronize a source that
   escapes through a relative path, symbolic link, or reparse point.
2. Load GitOps coordinates from the ignored deployment data or explicit parameters. The resolved
   ConfigMap must remain inside the configured GitOps checkout.
3. Run the script with `-WhatIf` first. Stop on invalid JSON, path escapes, wrong ConfigMap identity,
   a missing data key, unavailable `yq`, or an already-modified target.
4. Show only the source file name and the GitOps-relative target path. Never print, diff, summarize,
   or persist source values outside the target ConfigMap.
5. Obtain approval before real synchronization because it changes GitOps desired state.
6. Run the script without `-WhatIf`.
7. Inspect `git diff --stat` and confirm only the expected ConfigMap changed. Do not echo the full diff
   through a model-visible tool because it can contain private configuration and identifiers.
8. Run the GitOps repository's manifest validation and lint checks. Ask before running tests when
   required by shared workflow instructions.
9. Obtain separate approval before committing or pushing. Let Argo CD reconcile the committed change;
   never use `kubectl apply`, `edit`, `patch`, or `replace` to bypass GitOps.

## Safety Model

- The source file is borrowed private configuration. Never log its content, place it in command-line
  arguments, copy it to a temporary file, or include it in an issue, pull request, commit message, or
  progress summary.
- Source and target paths must remain inside their configured repositories without traversing a
   symbolic link or reparse point.
- The script passes only the source path to `yq` through a temporary process environment variable and
  removes that variable in a `finally` block. Source content is loaded directly from the file, so it
  is not constrained by Windows environment-variable size limits.
- The script performs no property filtering or value rewriting. It embeds the complete source JSON
   text in the configured ConfigMap key and verifies semantic equivalence afterward.
- A failed write or verification restores the original target bytes.
- Refuse to overwrite an existing uncommitted target change. Review or commit that change first.
- Synchronization is not deployment. A successful write only updates the local GitOps worktree.

## Script Reference

The script exits with code `0` when validation or synchronization succeeds and nonzero when an input,
target, tool, Git state, or semantic-equivalence check fails.

It validates:

- source JSON with comments and trailing commas enabled;
- target containment within the configured GitOps checkout;
- target `kind: ConfigMap`;
- configured target `metadata.name`;
- configured `data` key;
- semantic JSON equivalence after synchronization.

Run the synthetic Pester regression suite after changing the script:

```powershell
./.github/skills/sync-appsettings-to-configmap/scripts/Invoke-Tests.ps1
```

## Troubleshooting

| Symptom | Response |
| --- | --- |
| Deployment data is missing | Copy `deploy.local.psd1.example`, then supply private local values |
| `yq` is unavailable or wrong | Install the repository-supported Mike Farah `yq` binary and rerun `-WhatIf` |
| Source JSON is invalid | Fix the local file; do not modify the ConfigMap manually |
| Target is already modified | Review the existing GitOps change before synchronizing again |
| Semantic verification fails | Restore the target from Git and inspect the `yq` version and source syntax |
| Argo CD does not reconcile | Commit and push through the normal GitOps workflow, then inspect the Application |

> Brought to you by f2calv/SmartHaus
