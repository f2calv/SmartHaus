# Copilot Instructions

<!-- ── Synced section ─────────────────────────────────────────────────────
     This file plus every file under `.github/instructions/` is kept
     identical across all f2calv .NET repositories. The repo-specific
     "Project-Specific Overrides" section below is excluded from sync.
     Edit once, sync everywhere.
     ──────────────────────────────────────────────────────────────────── -->

## Instruction Files

Detailed conventions live in scoped instruction files under `.github/instructions/`, auto-applied by file type:

| File | Applies to | Covers |
| --- | --- | --- |
| `csharp.instructions.md` | `**/*.cs` | C# / .NET style, XML docs, logging, performance, Web API |
| `csharp.testing.instructions.md` | `**/*Tests/**/*.cs` | xUnit test structure, naming, theories, assertions |
| `csharp.mcp.instructions.md` | `**/*.cs` | MCP server tool attributes, descriptions, naming |
| `csharp.azure.instructions.md` | `**/*.cs` | Azure Table Storage & Redis key naming |
| `dotnet.instructions.md` | `**/*.csproj`, `*.slnx`, `Directory.*.props` | Central build/package config, solution format, SDK pinning |
| `github-actions.instructions.md` | workflows / `action.yml` | GitHub Actions naming, YAML, security, GitVersion |
| `documentation.instructions.md` | `**/*.md` | README consistency & Mermaid diagrams |
| `configuration.instructions.md` | `**/appsettings*.json` | `IAppConfig` / appsettings sync |

The conventions below always apply, regardless of the file being edited.

## Copilot Workflow

- **PII scan before committing**: Before staging or creating any local `git commit`, run the repository's PII scanner (`pwsh .scripts/Find-Pii.ps1`, or `-FailOnFind` as a hard gate) and remind the user to do the same. It seeds real values from the gitignored Local config files and flags any that have leaked into tracked files or history. Never commit if high-confidence (seed) PII is reported. The generated `pii-report*.csv` is gitignored and must never be committed.
- **Test execution**: Never run tests automatically — they may be integration tests requiring extra setup. Always prompt (ideally with a visual yes/no button) before running any tests.
- **Preserve git history during renames/moves**: When renaming or relocating files, first perform the rename/move (preferably via `git mv`), then make content edits to the file in its new location/name. This two-step approach preserves git history across the rename. Do not delete-and-recreate files when a rename or move is the intent.
- **Build after refactoring**: After any refactoring, build the **entire solution** (not just the affected project) to catch edge-case compilation errors in dependent projects. When multiple `.sln` / `.slnx` files exist, prefer the one with a `.Debug.slnx` suffix.

## Repository Structure

Every f2calv repository follows a consistent layout, regardless of language:

- **Root files**: `README.md`, `LICENSE`, `GitVersion.yml`, `.editorconfig`, `.gitattributes`, `.gitignore`, and `.pre-commit-config.yaml` live in the repository root.
- **Source code** lives under `src/`. *(Exception: GitHub Action repositories keep `action.yml` at the root per the GitHub Actions convention.)*
- **Tooling** lives in dot-prefixed folders — `.github/` (workflows, instructions), `.scripts/`, `.devcontainer/`, `.docker/`, `.config/`, `.vscode/`.
- **Additional documentation** beyond the root `README.md` lives as Markdown under `docs/`.
- **`.gitattributes`** standardises line endings across Windows/Linux. Use:

  ```gitattributes
  * text=auto eol=lf
  *.{cmd,[cC][mM][dD]} text eol=crlf
  *.{bat,[bB][aA][tT]} text eol=crlf
  ```

- **`.editorconfig`** is the single source of truth for indentation, line endings, and analyzer/formatting rules.
- **`GitVersion.yml`** in the root drives semantic-versioning rules.

## Misc

- When detecting new conventions or patterns in the codebase, add them to the appropriate `.github/instructions/*.instructions.md` file (or this file for cross-cutting workflow rules) and apply them retroactively where applicable.
- Keep this file and the `.github/instructions/` files in sync across repositories based on the common synced guidelines.

---

## Project-Specific Overrides

<!-- This section is excluded from cross-repository sync. Place any repo-specific rules below. -->

### Configuration File Strategy

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
