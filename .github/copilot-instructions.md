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
- After changing `appsettings.Local.json` for production deployment, synchronize it through the
  private GitOps repository's application-configuration skill.
