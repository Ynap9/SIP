---
name: write-markdown
description: Rules for writing/editing ANY Markdown (.md) in the SIPPACK repo — architecture docs, domain docs, plans, README, memory. Use BEFORE creating or editing a .md file so it follows the project's structure, language, size, and maintenance conventions. Applies to the main session and to agents.
---

# Skill: Writing Markdown files (SIPPACK)

Read before creating/editing any `.md`. Goal: every doc reads as one system — consistent language, structure, and upkeep.

## Language
- **All SIPPACK docs are written in ENGLISH** — agents read them faster, use fewer tokens, and follow them more reliably.
- Keep fixed Vietnamese domain/legal terms as-is (`Ho_so`, `Van_ban`, `TruongThongTin`, TT05 field names, mã hồ sơ).
- When editing an existing file, match its language; do not switch mid-file.
- Note: code comments stay Vietnamese (per `07-conventions.md`) — that is different from docs.

## Size — keep files small
- **Every `.md` must stay under 100 lines.** What to do when it grows past that depends on **how far over**:

| Over by | Action |
|---|---|
| **≤ 10 lines** | **DO NOT split.** Compress instead — tighten wording, merge related bullets/table rows. A file split off for a few lines only fragments one topic. |
| **> 10 lines** | **SPLIT into multiple files** (e.g. a `README.md` index + numbered sub-files, like `.claude/be/architecture/`). |

- One topic per file; link related files with relative paths.

### When you split, the parts MUST stay connected
A reader (human or agent) landing on one part has to know the rest exists and in what order to read it. Every split
set therefore needs **both**:
1. **An index** — a `README.md` (or the first file) listing the parts in reading order with a one-line purpose each.
2. **A continuation pointer at the end of each part** — e.g.
   `> **Next:** [02-steps.md](02-steps.md) — the ordered build steps for this plan.`
   and a back-link at the top: `> **Part 2 of 3** · previous: [01-requirements.md](01-requirements.md)`.

Never leave a part that reads as if it were the whole document — that is how an agent implements half a plan.

## Structure
- Exactly one `#` H1 title; a one-line purpose right under it (a `>` blockquote for canonical docs).
- `##` H2 sections, numbered if the file is long. Use **tables** for field / mapping / requirement specs.
- Fenced code blocks **always** declare a language (```csharp / ```ts / ```xml / ```bash).
- GitHub-flavored Markdown; relative internal links `[label](../path/file.md)`.

## Plans
- Location: `.claude/fe/docs/plans/<feature>.plan.md`, **≤100 lines**. A plan must be explicit and have:
  - **Requirements / Input** — what is given, preconditions, the expected INPUT.
  - **Steps** — ordered, small, each independently verifiable; BE steps end with `dotnet build`, FE with `npm run build`.
  - **Expected output** — the concrete OUTPUT/result wanted.
- Add a **Decision** note for each non-obvious tradeoff.

## Bug log
- When a bug is found (e.g. during review or while working), **record it** under `docs/bug/<slug>.md` so it is
  **not repeated**: summary, root cause, evidence, the fix, and a "how to prevent" section. Follow the existing
  `docs/bug/` style (see `config-header-unicode-normalization-mismatch.md`).

## Maintenance rules
- If a code change diverges from a pattern a doc describes, **update that doc in the SAME task**.
- Keep documented quirks intact (`Infratructure` misspelling, `docCode` vs `arcDocCode`, METS `TYPE=Mixed`) — do not "fix" them.
- New memory file: add exactly one pointer line in `MEMORY.md`; keep the fact in its own file.

## Before saving — checklist
1. English (or matching the file when editing)? **Under 100 lines** — if over by ≤10 compress, if over by >10 split into linked parts (index + next/previous pointers)?
2. One H1, clear H2s, code blocks have a language, relative links valid?
3. Plan: has Requirements/Input, Steps (build step last), Expected output?
4. Any other doc/pattern affected that must be updated too?
5. Found a bug? Logged in `docs/bug/`?
