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
- **Every `.md` must stay under 100 lines.** If it grows past that, **SPLIT into multiple files**
  (e.g. a `README.md` index + numbered sub-files, like `.claude/be/architecture/`).
- One topic per file; link related files with relative paths.

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
1. English (or matching the file when editing)? **Under 100 lines** (else split)?
2. One H1, clear H2s, code blocks have a language, relative links valid?
3. Plan: has Requirements/Input, Steps (build step last), Expected output?
4. Any other doc/pattern affected that must be updated too?
5. Found a bug? Logged in `docs/bug/`?
