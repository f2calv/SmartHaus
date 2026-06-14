# Squad Adoption — Human-Led AI Agent Teams for SmartHaus

> **Status**: Proposed — no Squad artefacts have been created yet. This document is the implementation plan; each task below carries a stable ID and a checkbox to tick as the work lands.

## Overview

[GitHub Squad](https://github.com/bradygaster/squad) is an open-source toolkit for running a **human-directed AI development team** through [GitHub Copilot](https://github.com/features/copilot). Instead of a single assistant switching roles, Squad scaffolds a team of specialists — typically a **lead**, **frontend**, **backend**, **tester**, and a coordinator (**Ralph**) — that "live" in the repository as Markdown files. Each member runs in its own context, reads only its own knowledge, and writes back what it learned, so the work stays inspectable and auditable with Git as the memory layer.

Squad ships as a Node CLI (`squad`, installed via `npm`) that scaffolds a `.squad/` directory of team state (members, charters, routing rules, decisions) and a `squad` agent for Copilot (CLI or VS Code). It coordinates issue triage, parallel execution, and PR handoffs while keeping humans accountable for priorities, approvals, and final changes.

SmartHaus is a mature, feature-flag-driven .NET 10 codebase with 30+ projects (see the [root README](../../README.md)). This is a textbook **brownfield** scenario: the application already exists, so the goal is **not** to generate it from scratch but to:

1. Install and operate Squad in a controlled, reproducible way (via the [VS Code dev container](../../.devcontainer/devcontainer.json)).
2. Stand up a **team charter** that captures the conventions already enforced by [`.github/instructions/`](../../.github/instructions) and [`copilot-instructions.md`](../../.github/copilot-instructions.md), so the agents inherit the repo's rules.
3. **Seed the team's knowledge from the existing application** so future change is coordinated against a known-good codebase rather than re-explained each session.
4. Drive a few example **"next steps"** (new features / enhancements) through the full *describe → triage → execute → review* loop.
5. (Optional) Wire Squad's **watch mode (Ralph)** and the **@copilot coding agent** into a **public GitHub Project** so triaged work becomes trackable issues and PRs.

### What Squad adds vs. what already exists

| Concern | Today in SmartHaus | With Squad |
| --- | --- | --- |
| Coding conventions | `.github/instructions/*.instructions.md` (auto-applied by file glob) | `.squad/` member charters reference the same rules — complements, does not replace |
| Feature design | Ad-hoc `docs/design/NNN-*.md` (this folder) | Natural-language tasking + per-member decision archives in `.squad/` |
| Task tracking | Manual checkboxes in design docs | GitHub Issues via triage, optionally polled and dispatched by watch mode (Ralph) |
| Agent context | `copilot-instructions.md` (single agent) | Team of scoped agents, each with persistent per-member history |
| Parallelism | One Copilot session at a time | Multiple specialists coordinate via file-backed state |

Squad is **additive**. The existing `docs/design/` design docs, instruction files, and `copilot-instructions.md` all remain the source of truth; the team charter distils them into the form Squad's members consume.

## How To Use This Document

Each task has:

- A **stable ID** (e.g. `SQ-1`) for cross-referencing in commits and PRs.
- A **priority** (`High` / `Medium` / `Low`).
- A checkbox to tick when complete.

Work the phases top-to-bottom. Phases 0–2 are setup (do once); Phase 3 seeds the brownfield team knowledge; Phase 4 onwards are repeatable per feature.

---

## Prerequisites

Squad's runtime requirements ([upstream Quick Start](https://github.com/bradygaster/squad#quick-start)):

- **Node.js (LTS)** and **npm** — Squad is distributed as `@bradygaster/squad-cli`
- **Git** — Squad uses the repo as its memory layer
- **[GitHub CLI (`gh`)](https://cli.github.com/)** — for Issues, PRs, and watch mode (Ralph)
- A supported coding agent — **GitHub Copilot** (CLI or VS Code), already the team standard

The dev container (Phase 1) provides all of these so nothing is installed on the host.

## Constraints & Decisions

- **Alpha software.** Squad is explicitly **experimental** — APIs and CLI commands may change between releases. Treat the integration as opt-in and easy to remove, and watch [`CHANGELOG.md`](https://github.com/bradygaster/squad/blob/dev/CHANGELOG.md) for breaking changes.
- **No host pollution.** Squad and its Node toolchain live **only** inside the dev container. The base image [`mcr.microsoft.com/devcontainers/dotnet:latest`](../../.devcontainer/devcontainer.json) does not ship Node, so it is added via a dev container feature (the `node` feature is already present but commented out in `devcontainer.json`).
- **Pin the version.** `npm install -g` accepts a version tag. Pin to a known Squad release (replace `X.Y.Z` below with the latest from [Releases](https://github.com/bradygaster/squad/releases)) so container rebuilds are reproducible. Bump deliberately via `squad upgrade --self`, never implicitly.
- **`squad init` on an existing repo.** Brownfield init must run *in place* and is idempotent, but must **not** clobber tracked files. Review the diff before committing. Prefer the guided `squad init` (no `--preset default`) so the team makeup is chosen deliberately for a brownfield .NET codebase.
- **Public-repo hygiene.** SmartHaus is a **public** repository. Per the cross-repo rules, nothing Squad writes (team charters, member knowledge, decisions, issues) may reference the private CAS repo, its classes, or its internals. Squad state describes SmartHaus only. Use `squad scrub-emails` before committing state to strip contributor emails.
- **Markdown lint.** Generated `.squad/` Markdown must pass the repo's `markdownlint` config (the dev container already installs `davidanson.vscode-markdownlint`). Spaced table separators (`| --- | --- |`) per the [documentation instructions](../../.github/instructions/documentation.instructions.md).

---

## Phase 0 — Spike & Validation (do first, throwaway)

- [ ] **`SQ-0` (High)** — In a scratch container or branch, run `squad init` against a **copy** of the repo to confirm: (a) exactly which directories Squad creates (`.squad/`, any `squad.agent.md`, `.github/` workflow files), (b) that it does not overwrite any tracked file, (c) the default team makeup and routing rules it proposes, and (d) the `squad` agent appears in Copilot. Record findings here, then discard the spike. This de-risks the in-place init on the real branch.

## Phase 1 — Dev Container Integration (controlled install)

Goal: make the `squad` CLI available automatically whenever the dev container starts, with zero host footprint.

- [ ] **`SQ-1` (High)** — Enable the **`node`** dev container feature in [`devcontainer.json`](../../.devcontainer/devcontainer.json) (currently present but commented out). Uncomment / add to the `features` block:

  ```jsonc
  "ghcr.io/devcontainers/features/node:1": {
      "version": "lts"
  }
  ```

  (`gh` is already provided by the existing `github-cli` feature, so no extra feature is needed for Issues/PRs/Ralph.)

- [ ] **`SQ-2` (High)** — Install the Squad CLI in [`postCreateCommand.sh`](../../.devcontainer/postCreateCommand.sh) so it is present on container create, pinned to a release version:

  ```sh
  # Squad (human-led AI agent teams) — pinned for reproducibility
  npm install -g @bradygaster/squad-cli@X.Y.Z
  ```

- [ ] **`SQ-3` (Medium)** — Add a readiness check to [`postStartCommand.sh`](../../.devcontainer/postStartCommand.sh) so each session confirms the tool is on `PATH` and healthy:

  ```sh
  squad doctor || echo "WARN: squad CLI not found or unhealthy — rebuild the dev container"
  ```

- [ ] **`SQ-4` (Low)** — Recommend the relevant VS Code extensions in the `customizations.vscode.extensions` array (Copilot Chat is required to select the **Squad** agent; markdownlint is already present). Ensure `github.copilot` and `github.copilot-chat` are listed if not already implied by the team's user settings.

- [ ] **`SQ-5` (Low)** — Document the dev container workflow in this repo's contributor docs (a short subsection under the README "Quick Start", or a `docs/` page): "Open in Dev Container → `squad` is ready → `gh auth login` → select the **Squad** agent in Copilot."

## Phase 2 — Initialise Squad In-Place

- [ ] **`SQ-6` (High)** — From inside the dev container, at the repo root, authenticate and initialise:

  ```sh
  gh auth login
  squad init
  ```

  Walk the guided setup (do **not** use `--preset default`) to choose a team makeup suited to a brownfield .NET 10 backend codebase. Review the resulting diff carefully. Expected new paths: `.squad/` (team, members, charters, routing, decisions) and possibly a `squad.agent.md` plus `.github/` workflow files. **Commit in a dedicated commit** ("chore: scaffold Squad") with nothing else mixed in, so the scaffold is easy to audit and revert.

- [ ] **`SQ-7` (Medium)** — Reconcile `.gitignore`. Decide what is tracked vs. ignored:
  - **Track**: `.squad/` team charters, routing, and decision archives (these are the value and the audit trail).
  - **Ignore**: per-user scratch, watch logs, and orchestration caches Squad emits (e.g. watch `--log-file` output, `ralph-stop` sentinel, scratch dirs — confirm during `SQ-0`).

- [ ] **`SQ-8` (Medium)** — Add a `.github/instructions/` note (or extend `copilot-instructions.md`) pointing future contributors at the new workflow: "Coordinated feature work flows through the **Squad** agent; team state lives in `.squad/`." This keeps the two instruction systems coherent.

## Phase 3 — Team Charter & Brownfield Knowledge Seeding

This is the heart of brownfield adoption: teach the Squad the rules, then point the team at the existing system.

### 3a. Team charter

- [ ] **`SQ-9` (High)** — Shape the team charters and routing rules, seeding them from the conventions **already documented** in the repo so the two never drift:
  - Code quality & style → [`csharp.instructions.md`](../../.github/instructions/csharp.instructions.md)
  - Testing standards → [`csharp.testing.instructions.md`](../../.github/instructions/csharp.testing.instructions.md)
  - Configuration / `IAppConfig` rules → [`configuration.instructions.md`](../../.github/instructions/configuration.instructions.md)
  - Documentation / README / Mermaid → [`documentation.instructions.md`](../../.github/instructions/documentation.instructions.md)
  - Cross-cutting workflow (test-execution gate, git-history-preserving renames, build-whole-solution, PII scan) → [`copilot-instructions.md`](../../.github/copilot-instructions.md)

  In the first Squad session, describe the platform so members inherit the rules:

  > I'm adopting Squad on an existing .NET 10 edge IoT platform: feature-flag-driven modular architecture, ILogger+Serilog logging, ConfigureAwait(false) in libraries, xUnit Unit/Integration test separation, IAppConfig four-tier appsettings strategy, README-must-stay-in-sync, no legacy/back-compat code, public-repo hygiene (no PII, no private-repo references). Set up the team to respect these conventions.

- [ ] **`SQ-10` (Medium)** — Cross-check the generated charters against the instruction files; remove anything Squad invented that contradicts existing conventions, and add any rule the description missed. The charter must be a **faithful distillation**, not a competing source of truth.

### 3b. Brownfield knowledge slice

The brownfield insight: have the team **learn one well-bounded module first**, so subsequent work is coordinated against real, understood code rather than the whole 30-project solution at once.

- [ ] **`SQ-11` (High)** — Choose the **first module for the team to learn**. Recommended: a single, self-contained, README-documented module rather than the whole app. Good candidates:
  - [`Comms`](../../src/CasCap.SmartHaus/README.md) — the Signal messaging + agent pipeline (rich behaviour, already has design docs [001](001-signalcli-audit-remediation.md)/[002](002-signalcli-receive-heartbeat.md)).
  - [`EdgeHardware`](../../src/CasCap.Api.EdgeHardware/README.md) — CPU/GPU telemetry (small, self-contained, demo-enabled).

- [ ] **`SQ-12` (High)** — In a Squad session, have the relevant members read the chosen module (README + source) and record what they learned into their decision archives — its purpose, feature flag, public surface, and patterns. Example:

  > Have the backend and tester members study the existing EdgeHardware feature: it monitors edge hardware telemetry (GPU via nvidia-smi, CPU temperature, Raspberry Pi GPIO), is feature-flag gated by `EdgeHardware`, exposes a REST snapshot endpoint and MCP tools, and writes to configurable sinks. Capture current behaviour — do not propose changes yet.

- [ ] **`SQ-13` (Medium)** — Review the captured `.squad/` knowledge for accuracy against the module README and source. This archive becomes living context the team reuses; fix any hallucinated behaviour before committing.

- [ ] **`SQ-14` (Low)** — Do **not** let the team change the module during knowledge seeding (the code already exists and works). The captured understanding is the deliverable for this phase. Optionally cross-link the module README under a "Squad knowledge" note.

## Phase 4 — Example "Next Steps" (the repeatable loop)

Demonstrate the full coordinated loop on genuine, small enhancements so the team builds muscle memory. Pick low-risk, well-scoped changes.

- [ ] **`SQ-15` (Medium)** — **Example A (enhancement to existing module).** Drive a real improvement through the team, e.g. a new EdgeHardware threshold-alert or an additional MCP tool. Describe the goal in natural language; let the lead split it, the backend implement, and the tester cover it:

  ```text
  Describe   <the new capability — what & why>
  Triage     <lead assigns work to the right members>
  Execute    <members coordinate via .squad/ state; backend reuses module patterns>
  Review     <human reviews the proposed diff and decisions before merge>
  ```

  Gate any execution behind the repo's **"never run tests automatically"** rule — review generated work before any test execution.

- [ ] **`SQ-16` (Low)** — **Example B (new module within the brownfield solution).** Have the team scaffold a brand-new small feature module (e.g. a new device integration stub) to show Squad's 0-to-1 flow inside the existing solution, respecting the established module layout (`CasCap.Api.<Name>` + `.Sinks` + `.Tests`, feature flag in [`FeatureNames`](../../src/CasCap.SmartHaus/Models/FeatureNames.cs)).

- [ ] **`SQ-17` (Low)** — Capture lessons in a short retrospective appended here (what the team got right/wrong, which charter rules needed tightening), and fold any reusable rule back into the `.squad/` charters and the relevant `.github/instructions/` file. Run `squad nap` periodically for context hygiene (compress/prune/archive).

## Phase 5 — (Optional) Watch Mode, @copilot & GitHub Project

> Addresses the follow-up idea: *a public SmartHaus GitHub Project to handle task creation and tracking for Squad-coordinated work.* Squad ships **watch mode (Ralph)** and an opt-in **@copilot coding agent** precisely for this.

The mechanism layers three GitHub concepts — don't conflate them:

| Concept | What it is | Role here |
| --- | --- | --- |
| **GitHub Issues** | Individual work items in the repo | What Ralph triages and what the @copilot agent picks up |
| **@copilot coding agent** | Autonomous Copilot member that branches and opens PRs | Optional standing team member, added via `squad copilot` |
| **GitHub Project** (Projects v2) | A board/table view that *aggregates* issues across a repo or org | A **tracking surface** for triaged issues — kanban/table, status columns, iteration fields |

- [ ] **`SQ-18` (Medium)** — Confirm watch mode is available in the installed Squad version (`squad triage --health`). It uses the [`gh` CLI](https://cli.github.com/), already installed via the `github-cli` dev container feature — so issue triage works from inside the container after `gh auth login`. Start in triage-only mode (no `--execute`) to observe before delegating execution.

- [ ] **`SQ-19` (Medium)** — Add the **@copilot coding agent** as an opt-in team member with `squad copilot` once the per-feature loop feels natural. Keep `--auto-assign` **off** initially so a human stays in the assignment loop; enable it only after trust is established.

- [ ] **`SQ-20` (Medium)** — Create a **public GitHub Project** ("SmartHaus — Squad") at the repo or `f2calv` org level with columns/status: `Todo → In Progress → In Review → Done`. Add a Project automation to auto-add issues labelled `squad` to the board. Standardise the `squad` **label** so the auto-add filter is reliable and generated issues are distinguishable from hand-written ones.

- [ ] **`SQ-21` (Low)** — Decide the **traceability convention**: each triaged issue and the PR that closes it should reference the originating Squad decision/session, keeping `decision ↔ issue ↔ PR` linked end-to-end. Configure watch's **overnight pause** (`--overnight-start` / `--overnight-end`) and `--state-backend git-notes` if you run Ralph unattended.

> **Caveat / honest framing:** the watch/@copilot/Project layer is **optional and last**. Get value from Phases 1–4 first (a single human + Squad agent in Copilot is plenty to start). Only graduate to Ralph, the autonomous @copilot member, and a Project once the coordinated loop feels natural and you actually want cross-feature visibility or unattended triage. Adding automation too early adds ceremony — and risk, given the alpha status — without payoff.

---

## Workflow Summary

```mermaid
flowchart TD
    subgraph Setup["One-time setup (Phases 1-3)"]
        DC([Dev Container starts]) --> NODE[node feature + npm installs squad CLI]
        NODE --> INIT[squad init]
        INIT --> CHARTER[Team charter from instruction files]
        CHARTER --> SEED[Seed knowledge of an existing module]
    end
    subgraph Loop["Per-feature loop (Phase 4, repeatable)"]
        DESC[/Describe task/] --> TRIAGE{Lead triages}
        TRIAGE --> EXEC[Members execute via .squad state]
        EXEC --> REVIEW[Human review & merge]
    end
    subgraph Track["Optional automation (Phase 5)"]
        RALPH[Watch mode - Ralph polls Issues] --> COPILOT[@copilot coding agent]
        COPILOT --> PROJECT[(Public GitHub Project)]
    end
    SEED --> DESC
    REVIEW -.optional.-> RALPH

    classDef setup fill:#e8f0fe,stroke:#4285f4
    classDef optional fill:#fff4e5,stroke:#f9a825
    class DC,NODE,INIT,CHARTER,SEED setup
    class RALPH,COPILOT,PROJECT optional
```

## Open Questions / Risks

- **`OQ-1`** — Exact directory layout `squad init` produces (`.squad/` contents, `squad.agent.md`, any `.github/` workflows) and which paths to track vs. ignore (resolved by `SQ-0`).
- **`OQ-2`** — Alpha churn: Squad is experimental and releases move fast. The pinned version in `postCreateCommand.sh` must be bumped deliberately via `squad upgrade --self`; `squad upgrade` then refreshes Squad-owned files without touching team state.
- **`OQ-3`** — Charter vs. instruction-file duplication: risk of two sources of truth diverging. Mitigation: treat `.github/instructions/` as canonical and the `.squad/` charters as a generated distillation; re-sync on any convention change (`SQ-10`, `SQ-17`).
- **`OQ-4`** — Autonomy boundaries: watch `--execute` and an auto-assigning @copilot member can act without per-step human approval. Mitigation: start triage-only, keep `--auto-assign` off, and honour the repo's "never run tests automatically" gate.
- **`OQ-5`** — Public-repo hygiene: every file Squad writes must be scanned for accidental PII, contributor emails (`squad scrub-emails`), or private-repo references before commit (same rule already applied to `appsettings*.json` and docs).

## References

- [Squad repository](https://github.com/bradygaster/squad) · [How Squad runs coordinated AI agents inside your repository (GitHub Blog)](https://github.blog/ai-and-ml/github-copilot/how-squad-runs-coordinated-ai-agents-inside-your-repository/) · [`CHANGELOG.md`](https://github.com/bradygaster/squad/blob/dev/CHANGELOG.md)
- [GitHub Copilot CLI](https://docs.github.com/en/copilot/github-copilot-in-the-cli) · [GitHub CLI (`gh`)](https://cli.github.com/) · [Node.js dev container feature](https://github.com/devcontainers/features/tree/main/src/node)
- Local context: [`copilot-instructions.md`](../../.github/copilot-instructions.md), [`.github/instructions/`](../../.github/instructions), [dev container](../../.devcontainer/devcontainer.json)
- Related design docs: [001 — SignalCli Audit Remediation](001-signalcli-audit-remediation.md), [002 — SignalCli Receive Heartbeat](002-signalcli-receive-heartbeat.md), [003 — Spec Kit Adoption](003-speckit-adoption.md)
