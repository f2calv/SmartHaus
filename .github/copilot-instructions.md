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

This repository has two tiers of `appsettings` files:

| File | Git-tracked | Purpose |
| --- | --- | --- |
| `appsettings.json` | Yes | Base/production configuration with **generic placeholder values** (no PII). Serves as a reference for open-source consumers to understand the full configuration surface and clone-and-run after a few tweaks. |
| `appsettings.Development.json` | Yes | Development/docker-compose overrides with **demo-friendly defaults** (e.g. `demo` credentials, Azurite connection strings, `HealthCheck: "None"`, `KeyVaultName: "skip"`). Allows `docker compose --profile demo up` to work out of the box. |
| `appsettings.Local.json` | No (`.gitignored`) | **Real production** secrets and configuration (Azure Key Vault names, storage account keys, device IPs, phone numbers, API tokens). Never committed. |
| `appsettings.Local.Development.json` | No (`.gitignored`) | **Real development** secrets and configuration (actual device passwords, real service endpoints). Never committed. |

**Loading order** (later files override earlier ones): `appsettings.json` → `appsettings.{env}.json` → `appsettings.Local.json` → `appsettings.Local.{env}.json` → Azure Key Vault.

**When adding, renaming, or removing an `IAppConfig` property**, update all four files:

1. `appsettings.json` — add/rename/remove the key with a generic placeholder value.
2. `appsettings.Development.json` — add/rename/remove with a demo-safe default if the property needs an override for local docker-compose runs.
3. `appsettings.Local.json` — add/rename/remove with the real production value.
4. `appsettings.Local.Development.json` — add/rename/remove with the real development value.

**PII rules**: The git-tracked files (`appsettings.json`, `appsettings.Development.json`) must **never** contain real IP addresses, hostnames, passwords, API keys, phone numbers, tenant IDs, storage account names, or any other personally identifiable information. Use generic placeholders (`192.168.1.100`, `example.com`, `mystorageaccount`, `+10000000000`, `demo`, `00000000-0000-0000-0000-000000000000`). Real values belong exclusively in the `.gitignored` Local files.
