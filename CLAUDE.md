# CLAUDE.md — SIPPACK

Guidance for AI agents working in this repo.

## Onboarding — READ THESE FIRST (before writing any code)
Do not start work until you have read and understood the docs relevant to your task:

1. **This file** — repo map, golden rules, build/run.
2. **Backend architecture** → [`.claude/be/architecture/`](.claude/be/architecture/) (README + numbered files: controllers, services, DTOs, domain, infra, shared, conventions).
3. **Frontend architecture** → [`.claude/fe/architecture/`](.claude/fe/architecture/) (README + pages, services, shared, layout, conventions).
4. **Frontend structure/UI/patterns** → [`.claude/fe/docs/`](.claude/fe/docs/): `project-structure.md`, `ui-guidelines.md`, `sakai-layout.md`, `skills/` (`feature-page`, `dialog-action`, `sakai-layout`), `plans/`.
5. **Domain standard** → [`docs/chuan-dong-goi-sip-tt05.md`](docs/chuan-dong-goi-sip-tt05.md) — the TT05/2025/TT-BNV SIP packaging spec **and** how the real sample package deviates from it (§8). The **real package is the source of truth**, not the raw TT05 tables.
6. **DB design** → [`docs/thiet-ke-db-dong-goi-sip.md`](docs/thiet-ke-db-dong-goi-sip.md) — proposed `HoSo`/`TaiLieu`/`DanhMuc` schema. **Proposal only — not yet implemented.** Today only `ConfigHoSo`/`ConfigVanBan` exist in the DB.

When your change alters a pattern any of these documents describe, **update that document** in the same task.

## Agents & session workflow
Start every session with the **`session-workflow`** skill (`.claude/skills/session-workflow/SKILL.md`). In short:
`sip-project-guide` loads project knowledge → tasks route to **`sip-be`** (`Sip.BE/**`) and/or **`sip-fe`**
(`Sip.FE/**`), BE first on full-stack work via a contract in `.claude/contracts/` → `/sip-review` calls
**`sip-reviewer`** (writes to `.claude/review/`) → `/sip-end` has `sip-project-guide` re-learn and report drift.
File ownership between `sip-be` and `sip-fe` is **exclusive**; neither edits the other's tree.

## What this is
SIPPACK — a **desktop** app to **check**, **pack**, **unpack** and **digitally sign** archival documents per the Vietnamese **TT05/2025/TT-BNV** SIP standard (E-ARK CSIP variant). One process hosts an ASP.NET Core API + an Angular SPA (Photino host); local **SQLite** DB.

## Repo map
```
Sip.BE/      ASP.NET Core (Clean Architecture) — Api, Application, Domain, Infratructure, Shared, External, Desktop
Sip.FE/      Angular 21 + PrimeNG (Aura, light-only) + Tailwind v4 (standalone + signals)
docs/        Domain/standard docs (TT05 spec, DB design, desktop packaging plan) — Vietnamese
.claude/     BE + FE architecture docs, and the moved FE docs/plans/skills
```

## The flows (BE ↔ FE mirror each other)
- **Config** — import Excel → `ConfigHoSo`/`ConfigVanBan`; list them (`pages/config`).
- **Check** — validate one record folder in 3 stages, **streamed realtime over SSE** (`pages/dong-goi/check`); **multi** validates a parent folder of many hồ sơ (`pages/dong-goi/multi`).
- **Pack** — build a SIP package (EAD.xml + METS.xml + ZIP); **nothing persisted to DB** (`pages/dong-goi`); **multi** packs a parent folder (SSE).
- **Unpack** — reverse a SIP `.zip`/folder back into a hồ sơ folder + Excel (`pages/boc-tach`).
- **Sign** — pick a certificate (incl. USB token), gate PDF/A, sign a folder with a Ban Cơ yếu RFC 3161 timestamp, SSE per file (`pages/ky-so`).

Multi + Unpack rules: [`docs/multi-unpack-flows.md`](docs/multi-unpack-flows.md). Signature/timestamp: [`docs/verify-signature-timestamp.md`](docs/verify-signature-timestamp.md).

## Golden rules
1. **Do exactly what is asked** — don't refactor or change architecture on your own.
2. Follow `07-conventions.md` (BE) / `05-conventions.md` (FE); reuse the FE **skills** for pages/dialogs/layout.
3. Keep BE DTOs and FE `models/*.models.ts` in sync — JSON is **camelCase**; every REST call returns HTTP 200 with real status in `ApiResponse.status` (1=ok, 0=error). Gate FE reads on `isResponseSucceed(res)`.
4. Keep existing quirks: misspelled `Infratructure` project/namespace, `Domain.Document` vs `Documents` split, real-package deviations (`docCode` not `arcDocCode`, METS `TYPE=Mixed`, rep1 `OBJID=rep1`, no `source` field, ZIP name = plain GUID).
5. Plans go in `.claude/fe/docs/plans/<feature>.plan.md`, ≤100 lines, with **Requirements / Steps / Expected output**; get them approved before building.
6. Every FE component has a `<name>.spec.ts` smoke test. End BE build steps with `dotnet build`, FE with `npm run build`.

## Build / run
- BE: `dotnet run` in `Sip.be.Api` (dev API on `http://localhost:5145`); desktop build via `Sip.BE/build-desktop.ps1`.
- FE: `npm start` (ng serve) in `Sip.FE`.
