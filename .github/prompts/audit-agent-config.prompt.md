# Audit Agent Config

Audit and synchronise all AI agent configurations in `appsettings*.json` to ensure
they follow the conventions established in this repository. Do **not** change agent-specific
domain instructions — only ensure the shared rules below are present and consistent.

## Scope

Scan every agent entry under `CasCap:AIConfig:Agents` in all `appsettings*.json` files.

---

## Part A — Agent ↔ Tool Audit Matrix

Before checking the sync rules, produce a comprehensive inventory of the agent/tool landscape.

### A1. Discover All Available Tools

1. Find every `[McpServerToolType]` class under `src/` (the `*McpQueryService.cs` files).
2. For each service, list every `[McpServerTool]` method and its snake_case tool name.
3. Also record any remote `Endpoint` entries in `appsettings.json` — these expose tools
   that are not in the local codebase but are still part of the tool surface area.

### A2. Build the Agent → Tool Matrix

For each agent in `appsettings.json`, resolve its effective tool set:

- **`Service` without filters** — all tools from that service.
- **`Service` with `IncludeTools`** — only the listed tools.
- **`Service` with `ExcludeTools`** — all tools minus the listed ones.
- **`Endpoint` without filters** — all tools from that remote endpoint (list as
  "remote: {url}" since individual tools cannot be enumerated from config alone;
  note any `IncludeTools`/`ExcludeTools` if present).
- **`Agent` references** — record delegation links (not direct tools).

### A3. Output the Matrix

Print a markdown table to the Copilot output with these columns:

| Agent | Service / Endpoint | Tool (snake_case) | Source |
| --- | --- | --- | --- |

Where **Source** is `local` for `[McpServerTool]` methods or `remote` for `Endpoint` entries.

After the per-agent table, print a second table of **unassigned tools** — any local
`[McpServerTool]` method that does not appear in any agent's effective tool set:

| Unassigned Tool | Service | Suggested Agent | Reason |
| --- | --- | --- | --- |

For each unassigned tool, suggest which existing agent (or a new agent) should own it
and give a brief reason.

### A4. Agent Delegation Summary

Print a third table showing the delegation graph:

| Parent Agent | Delegates To | Via |
| --- | --- | --- |

Where **Via** is `{ "Agent": "..." }` in the parent's `Tools` array.

### A5. Stale Tool Name Validation

Cross-reference every snake_case tool name in `IncludeTools` and `ExcludeTools` arrays
across all `appsettings*.json` files against the actual `[McpServerTool]` methods
discovered in A1. Flag any tool name that does not match a real method:

| Agent | Array | Stale Tool Name | Suggestion |
| --- | --- | --- | --- |

Where **Suggestion** is the closest matching tool name if a rename is likely, or
"remove — tool no longer exists" if the method was deleted.

---

## Part B — Mermaid Diagram

Generate (or update) a Mermaid `flowchart TD` diagram that visualises:

1. **Every agent** as a node (use stadium shape `([ ])` for the top-level `CommsAgent`,
   rectangles for specialist agents, and dashed borders for disabled agents).
2. **Delegation arrows** from parent → sub-agent (solid lines, labelled `delegates`).
3. **Tool service subgraphs** — group each `*McpQueryService` / remote endpoint as a
   subgraph containing its individual tools. Draw arrows from agents into the subgraphs
   they consume.
4. **Shared services** (`SystemMcpQueryService`, `MessagingMcpQueryService`) shown as a
   single shared subgraph with dotted arrows from all agents.
5. **IncludeTools / ExcludeTools** — when an agent only uses a subset of a service's
   tools, draw the arrow to the specific tool nodes (not the subgraph).
6. **Disabled agents** — style with a dashed border and a `disabled` label.

Place the diagram in a fenced `mermaid` code block inside the project's
`src/CasCap.SmartHaus/README.md` file under a `## Agent Architecture` heading. If the
heading already exists, replace the existing diagram.

---

## Part C — Sync Rules

### 1. Poll Rules (all agents)

Every agent that has `MessagingMcpQueryService` in its `Tools` array **must** include
the following poll rules in its `Instructions` string (appended after the agent's
domain-specific instructions):

> Poll rules: 1) When presenting choices, ONLY use the create_poll tool — NEVER list
> options in text. 2) After creating a poll, reply with ONE short sentence only — do NOT
> repeat or list the options. 3) When a poll vote arrives, call close_poll first, then act
> on the chosen option — do NOT present more choices unless they are in a new poll.
> 4) Only offer options you can actually execute with your available tools — do NOT suggest
> actions you have no tool to perform.

If an agent does **not** have `MessagingMcpQueryService` in its tools, flag it — every
agent reachable from the Comms Agent fan-out should have poll capabilities.

### 2. MessagingMcpQueryService Presence

Every sub-agent referenced via `{ "Agent": "..." }` in any parent agent's `Tools` array
must include `{ "Service": "MessagingMcpQueryService" }` in its own `Tools` so it can
create polls autonomously.

### 3. Sub-Agent Tool Overlap

When a parent agent delegates to a sub-agent via `{ "Agent": "..." }`, the parent should
**not** also have the sub-agent's specialist tools registered directly **with overlapping
tool names** — that causes the parent to call the tool itself instead of delegating.

Multiple agents **can** reference the same `Service` (e.g. `FrontDoorMcpQueryService`)
as long as their `IncludeTools` / `ExcludeTools` filters ensure **no individual tool name
appears on both the parent and the sub-agent**. Flag any tool name that is reachable from
both sides.

For example, if `SecurityAgent` has `FrontDoorMcpQueryService` (all tools), the
`CommsAgent` must **not** also list `FrontDoorMcpQueryService` without an `ExcludeTools`
that removes every tool the `SecurityAgent` already exposes. Conversely, if the
`CommsAgent` includes only `get_house_door_state` via `IncludeTools` and the
`SecurityAgent` excludes that same tool, there is no overlap and both entries are valid.

### 4. Duplicate Tool References

Within a single agent's `Tools` array, each `Service` or `Endpoint` entry should appear
**at most once**. Flag any duplicates. The following shared services are **exempt** from
cross-agent duplicate checking (they are intentionally present on every agent) but must
still not appear twice on the *same* agent:

- `SystemMcpQueryService`
- `MessagingMcpQueryService`

### 5. SystemMcpQueryService Presence

Every agent must include `{ "Service": "SystemMcpQueryService" }` in its `Tools` array
so it has access to the current date/time.

### 6. Vision-Capable Agents

Agents that receive image content (currently `SecurityAgent`) must include a vision
statement in their `Instructions`:

> You have vision capabilities and can analyse images sent to you.

Without this, the model defaults to claiming it cannot process images.

### 7. Description Must Advertise Capabilities

Each agent's `Description` is read by **other agents** to decide whether to delegate.
It must summarise the agent's available tooling so parent agents can make informed
routing decisions. Specifically:

- List the key **actions** the agent can perform (e.g. "door camera images, front door
  state queries, turning the front door light on or off").
- Mention **poll capabilities** if `MessagingMcpQueryService` is present (e.g. "can
  present choices to the user via polls").
- Keep it to one or two sentences — concise but complete enough that a parent agent
  (or LLM) knows what to delegate here vs. elsewhere.
- When tools are added or removed from an agent, update the `Description` in the same
  change.

### 8. Instruction Structure

Each agent's `Instructions` should follow this structure:

1. **Domain role** — one or two sentences describing the agent's specialist area
2. **Vision statement** — if the agent processes images (see rule 6)
3. **Poll rules** — the standardised block from rule 1

---

## Part D — README Sync

Using the data collected in Parts A and B, update the `src/CasCap.Common.AI/README.md` file:

### D1. Services Table

Update the `## Services` table with accurate tool and prompt counts per service. Count
the actual `[McpServerTool]` methods in each `*McpQueryService.cs` file — do not rely on
previously hardcoded numbers.

### D2. Agent Architecture Diagram

Update the `## Agent Architecture` Mermaid diagram (from Part B) to reflect the current
`appsettings.json` tool assignments. Specifically:

- Every service subgraph must list **only** the tools that actually exist on that service
  (cross-reference with A1).
- Agent → subgraph arrows must match the current `Tools` array in `appsettings.json`.
- When an agent uses `IncludeTools`, draw arrows to the specific tool nodes inside the
  subgraph. When an agent uses the full service (no filter or `ExcludeTools`), draw the
  arrow to the subgraph itself.
- Remove any subgraph nodes for tools that have been renamed, moved, or deleted.

### D3. Agent Tools Summary Table

Add or update an `## Agent Tools Summary` section **after** the Agent Architecture
diagram. This table provides a quick-reference of each agent's effective tool set:

| Agent | Direct Tools | Via Delegation | Total |
| --- | --- | --- | --- |

Where:
- **Direct Tools** — count of tools directly assigned via `Service` entries (after
  applying `IncludeTools` / `ExcludeTools`).
- **Via Delegation** — count of tools reachable through `{ "Agent": "..." }` delegation
  (sum of the sub-agent's direct tools, recursively).
- **Total** — sum of Direct + Via Delegation. Note that shared services
  (`SystemMcpQueryService`, `MessagingMcpQueryService`) are counted per agent.

---

## Output

### Part A output

Print the tables from sections A3, A4 and A5 to the Copilot chat output.

### Part B output

Create or update the Mermaid diagram in `src/CasCap.Common.AI/README.md` as described.

### Part C output

For each agent, report:
- ✅ Compliant — no changes needed
- ⚠️ Missing — what needs to be added (with the exact text)
- 🔄 Overlap — tools that should be removed from a parent agent
- 🔁 Duplicate — the same service/endpoint appearing more than once on a single agent
- 📝 Description — if the `Description` does not accurately reflect the agent's current tooling
- 🔗 Stale — any `IncludeTools`/`ExcludeTools` entries referencing tool names that no longer exist

Then apply all fixes to the `appsettings*.json` files.

### Part D output

Apply all README updates described in D1, D2 and D3 to `src/CasCap.Common.AI/README.md`.
