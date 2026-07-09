---
name: task-workflow
description: How to run any multi-step task in SIPPACK — batch ALL clarifying questions into ONE round up front, then keep a visible task checklist and mark each item done as you finish it. Log any bug found so it is not repeated. Applies to the main session AND every spawned agent. Use at the start of any non-trivial task.
---

# Skill: Task execution workflow (SIPPACK)

Applies to the main session and EVERY spawned agent. Two phases: clarify once, then track visibly.

## Phase 1 — Ask questions once (batch)
- Before starting, collect **ALL** open/blocking questions and ask them **together in a single round**
  (one `AskUserQuestion` call, up to 4 questions).
- Do **NOT** drip questions one at a time across turns.
- Only ask what genuinely changes what you do. For anything with a sensible default, pick it, state it,
  and proceed — do not ask.
- If nothing is blocking, skip asking and go straight to Phase 2.

## Phase 2 — Track work visibly
- Break the task into concrete steps and create a **visible task list** (`TaskCreate`) so the user sees the plan.
- Exactly **one** item `in_progress` at a time; set it `in_progress` when you start it (`TaskUpdate`),
  and `completed` the moment it is done.
- Keep the list current — add steps discovered mid-task, remove obsolete ones.
- Mark items done **as you go**, not all at the end.

## During review/execution — log bugs
- If you find a bug (in review or while working), **record it** under `docs/bug/<slug>.md`
  (root cause + how to prevent) so it is **not repeated**. See the `write-markdown` skill for the bug-log format.
- Then fix it, or add a task item for the fix.

## Delegating to agents
When spawning an agent (Agent tool), put in its prompt an instruction to follow **this** workflow:
1. surface all clarifying questions back **at once** (never piecemeal),
2. maintain and report a **checklist** of sub-tasks and mark each one done,
3. **log any bug** found under `docs/bug/`.

This propagates the same discipline to sub-agents.

## Quick checklist
1. Gathered and asked all questions in one round (or confirmed none needed)?
2. Created a visible task list; exactly one `in_progress`; marking done as I go?
3. Logged any bug found in `docs/bug/`?
4. If I delegated, did the agent prompt carry these three rules?
