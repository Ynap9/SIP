# Sip.FE — Frontend Architecture (read before coding)

> Canonical doc for humans & agents. **Read this folder before adding/changing FE code.** For the full folder tree and stack details, also see [`../docs/project-structure.md`](../docs/project-structure.md). Do not restructure the app on your own.

## Stack
- **Angular 21** — standalone components (NO NgModules), **signals** (`signal`, `input`, `computed`).
- **PrimeNG 21** (+ `@primeng/themes/aura`) — primary UI kit, barrel-exported in `shared/import.shared.ts`.
- **Tailwind CSS v4** — global in `src/styles.css`.
- **RxJS** for HTTP/streaming; **moment** for dates.
- Final target: **desktop app** (Photino host) served same-origin with the API.

## App shape
```
src/
├── main.ts / app.ts / app.config.ts / app.routes.ts   # bootstrap (files live in src/, not src/app/)
└── app/
    ├── layout/       # shell: layout, topbar, sidebar, footer, menu + LayoutService
    ├── pages/        # home, config, dong-goi/{check,multi}, boc-tach, ky-so — lazy-loaded via *.router.ts
    ├── services/     # config, check, multi-check, pack, multi-pack, unpack, sign, desktop (root-provided)
    ├── models/       # per-feature interfaces (config, check, multi-check, pack, multi-pack, unpack, sign)
    └── shared/       # BaseComponent, SharedImports, data-table, file-tree, utils, models, directives, constants
```

## Document index
| File | Content |
|------|---------|
| [01-pages-routing.md](01-pages-routing.md) | Pages, lazy routing, feature-page pattern |
| [02-services-http.md](02-services-http.md) | HTTP services, API base URL, SSE, DesktopService |
| [03-shared.md](03-shared.md) | BaseComponent, SharedImports, DataTable, Utils, models |
| [04-layout.md](04-layout.md) | Shell layout + LayoutService |
| [05-conventions.md](05-conventions.md) | Signals, standalone, naming, response handling |

## The UI flows (mirror the BE)
- **Home** (`pages/home`) — landing.
- **Config** (`pages/config`, `/cau-hinh`) — view ConfigHoSo / ConfigVanBan tables.
- **Check + Pack** (`pages/dong-goi`, `/dong-goi`) — `check` = one hồ sơ, `multi` = a parent folder; pick a folder, stream results (SSE), then pack. Both routes carry `data: { reuse: true }` (see `shared/app-route-reuse.strategy.ts`).
- **Sign** (`pages/ky-so`, `/ky-so`) — 3-step wizard: `cert-step` (pick certificate/token) → `config-step` (PDF preview with `pdfjs-dist`, drag-drop the signature position, pages-to-stamp, overlap option) → `sign-step` (PDF/A gate + sign, SSE).
- **Unpack** (`pages/boc-tach`, `/boc-tach`) — reverse a SIP `.zip`/folder, browse the result with `shared/components/file-tree`.
