# Copilot Instructions

## Shared Instructions

Shared Copilot instructions, skills and prompts are maintained centrally in the [.github](https://github.com/f2calv/.github) repository, under `.github/instructions/`, `.github/skills/` and `.github/prompts/`. They are deliberately not copied into this repository, so a change there takes effect everywhere without a pull request here.

To load them, clone that repository and either add it to this VS Code workspace, or link its folders into `~/.copilot/`. Its README explains both.

If those shared files are not visible, stop and tell the user rather than guessing the conventions — this repository depends on them.

Everything below is specific to this repository.

## Public Deployment Boundary

This public repository stops at building and publishing application, package,
and Helm artifacts. Do not include deployment-environment identifiers, manifest
locations, environment or namespace names, cluster state, deployed versions,
or operational procedures in tracked files or public pull requests.

## KNX Export Source Of Truth

- KNX project software is the source of truth for group-address names, locations, orientations,
  device types and semantics. Correct programming defects there, then generate and import a fresh
  export.
- Never hand-edit a generated KNX export to correct or conceal a programming defect. A manual edit
  would drift from the KNX project and be overwritten by the next export.
- Application parsing and classification may reflect explicit metadata from the export, but must not
  compensate for known incorrect metadata in code.

## NuGet Package Holds

- `Asp.Versioning.Mvc` has target-framework-specific major ceilings: retain the latest compatible
  8.x version for `net8.0` projects and the latest compatible 10.x version for `net10.0` projects.
  The 10.x package targets .NET 10 and must not replace the conditioned 8.x entry while SmartHaus
  multi-targets `net8.0`.
- Keep the `Asp.Versioning.Mvc` `PackageVersion` conditions in `Directory.Packages.props`. A full
  dependency update may advance each framework-compatible line independently, but must not collapse
  them into one unconditional version.
- `Asp.Versioning.Mvc.ApiExplorer` has a 10.x major ceiling matching the server project's .NET 10
  target. Reassess the family only when the corresponding application target framework changes.

## Configuration File Strategy

SmartHaus follows the standard provider order and options-synchronisation rules in
`dotnet.configuration.instructions.md`. Its repository-specific split is:

| File | Git-tracked | Purpose |
| --- | --- | --- |
| `appsettings.json` | Yes | Base and production reference configuration with public-safe placeholders |
| `appsettings.Development.json` | Yes | Demo-safe local and container-development overrides |
| `appsettings.Local.json` | No | Private values shared by local environments and used as production deployment input |
| `appsettings.Local.Development.json` | No | Private overrides used only in Development |

- Keep tracked configuration clone-and-run friendly and free of real identifiers, endpoints and
  credentials. Use synthetic placeholders.
- Keep real private values in the gitignored local files. Azure Key Vault is added as the final
  application-specific provider and remains the authority for deployment credentials.
- When a bindable property changes, follow the central synchronisation rule across all four existing
  tiers; environment and local files restate only values that differ from earlier providers.
- After changing `appsettings.Local.json` for production deployment, use this repository's
  `sync-appsettings-to-configmap` skill to update the caller-configured private GitOps repository.

## Helm Charts

`charts/smarthaus` is published by `deploy-all.yml` under the application release version; its
`Chart.yaml` version is a placeholder. `charts/smarthaus-dashboards` is versioned by its own
`Chart.yaml`: a default-branch change under that directory runs `deploy-dashboards.yml`, which
publishes that version and bumps the dashboard Application in the private GitOps repository named
by the `GITOPS_REPOSITORY`, `SMARTHAUS_DASHBOARD_MANIFEST_PATH`, `SMARTHAUS_DASHBOARD_NAMESPACE` and
`SMARTHAUS_DASHBOARD_ENVIRONMENT` repository variables. Bump the dashboard chart version with every
packaged change, including README-only edits. For local iteration, `deploy.ps1 -OnlyCharts`
publishes a disposable development version without rolling application pods.

Every chart keeps chart-testing fixtures under `ci/`, and the `helm` job in `ci.yml` lints the chart
against each of them. Dashboard JSON is never passed through Helm `tpl`, because Grafana legend
tokens use the same double-brace syntax; datasource UIDs are substituted with exact `replace` calls.
The Fronius dashboard's ConfigMap name and data key are pinned in the template to preserve its
identity; keep new dashboards on the file-basename convention.
