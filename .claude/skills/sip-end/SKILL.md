---
name: sip-end
description: Close a SIPPACK session — have the sip-project-guide agent re-read all docs, plans, code, bugs, reviews and skills, and report what changed since its briefing so the next session starts from current state. Invoked by the user as /sip-end. Use only when the user asks.
---

# Skill: /sip-end

Consolidate what this session changed, so the next one does not start from a stale picture. **User-triggered only.**

## Steps
1. **Snapshot the session's real footprint** — do not rely on memory:
   ```bash
   git status --short
   git diff --stat
   ```
   Plus any untracked new folders (`.claude/review/`, `.claude/contracts/`, new plans).
2. **Call `sip-project-guide`** and ask it to re-learn and report drift, giving it that footprint. It must cover:
   - `CLAUDE.md`, `.claude/be/architecture/*`, `.claude/fe/architecture/*`, `.claude/fe/docs/*`
   - `.claude/be/plans/*`, `docs/plans/*`
   - `docs/*.md` (domain, signature, CA trust, readiness/libraries/appearance)
   - `docs/bug/*.md` — which are still live, which the code has since fixed
   - `.claude/review/*.md` — which findings are still open
   - `.claude/skills/*` — the rules currently in force
   - the code under `Sip.BE/` and `Sip.FE/` for anything the docs do not yet describe
3. **Require it to report, specifically**:
   - **Doc/code drift** — where a doc now contradicts the code. Code wins; the doc is the defect.
   - **Open review findings** carried into the next session.
   - **Plans**: which steps are done, which are not started.
   - **New decisions** made this session that are not yet written down anywhere.
4. **Relay the summary** to the user, and offer to fix any drift it found — do not fix silently.

## Rules
- This is a **read-and-report** phase. `sip-project-guide` is read-only; it must not edit files.
- Anything worth carrying forward but not yet recorded should become a doc/plan/bug entry **before** the session
  ends — an insight that lives only in a chat transcript is lost.
- If drift is found between a doc and the code, that is a defect in the doc: say which file and which line.
