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
    ├── pages/        # feature pages (home, config, dong-goi/…) — lazy-loaded via *.router.ts
    ├── services/     # HTTP + desktop bridge services (root-provided)
    ├── models/       # per-feature interfaces (config, check, pack)
    └── shared/       # BaseComponent, SharedImports, data-table, utils, models, directives, constants
```

## Document index
| File | Content |
|------|---------|
| [01-pages-routing.md](01-pages-routing.md) | Pages, lazy routing, feature-page pattern |
| [02-services-http.md](02-services-http.md) | HTTP services, API base URL, SSE, DesktopService |
| [03-shared.md](03-shared.md) | BaseComponent, SharedImports, DataTable, Utils, models |
| [04-layout.md](04-layout.md) | Shell layout + LayoutService |
| [05-conventions.md](05-conventions.md) | Signals, standalone, naming, response handling |

## The three UI flows (mirror the BE)
- **Config** (`pages/config`) — view ConfigHoSo / ConfigVanBan tables.
- **Check + Pack** (`pages/dong-goi`) — pick a folder, stream check results (SSE), then pack.
- **Home** (`pages/home`) — landing.
