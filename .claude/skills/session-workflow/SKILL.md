---
name: session-workflow
description: How a SIPPACK working session is orchestrated end to end — load project knowledge at session start, route each task to the sip-be / sip-fe agents (and keep them from conflicting), review on demand, and consolidate knowledge at session end. Use at the START of every session, and whenever deciding which agent should handle a task.
---

# Skill: Session workflow (SIPPACK)

Four phases. The main session is the **orchestrator** — it routes work, it does not do the work itself when an
agent owns that area.

## Phase 1 — Session start: load knowledge
Call the **`sip-project-guide`** agent once, before touching anything:

> "Brief me on SIPPACK" — or a targeted question if the task area is already known.

Then read `.claude/review/` for open findings. Do not start work on a stale mental model.

## Phase 2 — Task routing
Decide from the task, not from habit:

| Task touches | Call |
|---|---|
| `Sip.BE/**` only | **`sip-be`** |
| `Sip.FE/**` only | **`sip-fe`** |
| both | **`sip-be` first**, then **`sip-fe`** — see below |
| docs / plans only | main session (load `write-markdown` first) |

Pass each agent **the user's request verbatim**, not a paraphrase, plus the plan path if one exists.

### Full-stack tasks — the anti-conflict protocol
1. **`sip-be` runs first** and publishes `.claude/contracts/<feature>.contract.md` (routes, DTO JSON shapes in
   camelCase, enum numbering, SSE event shapes).
2. **`sip-fe` runs second** and implements against that contract — never against the BE source, never guessing.
3. The two talk via **`SendMessage`** for clarifications and any contract change.
4. **File ownership is exclusive**: `sip-be` writes only `Sip.BE/**`, `sip-fe` only `Sip.FE/**`. Neither edits the
   other's tree, not even a one-line DTO rename.
5. **The contract wins** any disagreement, until `sip-be` updates it.

> Why serialise instead of running both at once: BE and FE must agree on field names, and the FE cannot write the
> right model until the DTO exists. Running them in parallel means the FE guesses — which is exactly the
> camelCase / enum-shape breakage this repo has hit before. The BE half is also the shorter half.

## Phase 3 — Review (only on request)
**Only when the user asks** (`/sip-review`, or "review it"): call **`sip-reviewer`** with the user's original
request verbatim plus the changed paths. It writes `.claude/review/<yyyyMMdd-HHmm>-<slug>.md` and reports the
verdict. Do **not** auto-review after every task — the user decides.

Findings then go back to whichever agent owns the area; the fixing agent marks the row `✅ FIXED` in the review file.

## Phase 4 — Session end (only on request)
**Only when the user asks** (`/sip-end`): call **`sip-project-guide`** to re-read docs, plans, code, `docs/bug/`,
`.claude/review/` and the skills, and to report **what changed since its own briefing** — especially doc/code drift
and any review finding still open. That consolidated state is what the next session starts from.

## Rules that hold across all phases
- Every agent loads **`task-workflow`** (visible checklist, questions batched once) and **`write-markdown`** before
  writing any `.md`.
- Any bug found in any phase is logged to `docs/bug/<slug>.md` so it is not repeated.
- Agents report in **Vietnamese**; docs are written in **English**.
- Do not spawn an agent for a task the main session can finish in a couple of edits — the routing table is a
  guideline for real work, not a ceremony.
