# Sip.FE Project Structure

> Canonical doc for humans & agents. **Read this before creating new code** to follow the project's real structure and conventions.

## Parts (read in this order)

| Part | File | Content |
|---|---|---|
| 1 | **this file** | Stack, bootstrap, the real folder tree, build status |
| 2 | [shared-and-conventions.md](shared-and-conventions.md) | `shared/` API, code conventions, providers, where new code goes, enums-vs-models |
| 3 | [backend-integration.md](backend-integration.md) | Base URL, envelope, SSE, the BE endpoint list, the Photino desktop host |

Related: [`../architecture/`](../architecture/) (per-area rules), [`ui-guidelines.md`](ui-guidelines.md), [`skills/`](skills/).

## 1. Stack & libraries

- **Angular 21** — standalone components (NO NgModules), **signals** (`signal`, `input`, `computed`).
- **PrimeNG 21** — primary UI kit (table, dialog, toast, button, form controls...), preset **Aura, light-only**. Barrel-exported in `shared/import.shared.ts`.
- **Tailwind CSS v4** — `@import "tailwindcss";` in `src/styles.css`. No `tailwind.config.js`; PostCSS wires `@tailwindcss/postcss`. Use utility classes for layout/spacing only.
- **@ng-icons/heroicons** — icon set declared in `src/config/icons.ts`, **not currently wired** (the UI uses `pi pi-*` PrimeIcons).
- **moment** — date formatting (used by `Utils`).
- **pdfjs-dist** — render PDF pages to a canvas in `pages/config/template-chu-ky/template-config` (chọn vị trí chữ ký). `angular.json`
  copies `pdf.worker.min.mjs` to the build root and the app points at it with
  `new URL('pdf.worker.min.mjs', document.baseURI)` — **never a CDN** (the app runs offline) and **never
  `import.meta.url`**, which resolves to a bare specifier the bundler drops in a production build
  (`docs/bug/pdfjs-worker-bare-specifier-not-bundled.md`).
- **vitest** (`@angular/build:unit-test`) — not Karma.
- Component styles: `.scss` for pages & shared components, `.css` for layout. Global = Tailwind in `src/styles.css`.
- Target: **desktop app hosted by Photino/WebView2**, same-origin with the API — see [part 3](backend-integration.md).

## 2. Bootstrap & root

- `src/main.ts` → `bootstrapApplication(App, appConfig)`.
- **Root app files live directly in `src/`** (NOT `src/app/`):
  - `src/app.ts` — `App` (selector `app-root`), contains only `<router-outlet />`.
  - `src/app.html`, `src/app.css`, `src/app.spec.ts`.
  - `src/app.config.ts` — `appConfig` (providers).
  - `src/app.routes.ts` — routes; route `''` = `Layout` (shell) with `children` awaiting pages.
- `src/app/` holds feature & shared code (`layout/`, `shared/`).
- Build configurations in `angular.json`: `production` (default), `desktop` (swaps in `environment.desktop.ts`), `development`.

## 3. Actual folder tree

```
src/
├── main.ts
├── app.ts / app.html / app.css / app.config.ts / app.routes.ts / app.spec.ts
├── styles.css                      # Tailwind global
├── index.html
├── config/
│   └── icons.ts                    # heroIcons map (@ng-icons/heroicons/outline)
├── environments/
│   ├── environment.ts              # prod  (baseUrl: http://localhost:5145)
│   ├── environment.development.ts  # dev
│   └── environment.staging.ts
└── app/
    ├── layout/                     # app-layout (topbar + nav tab strip + footer + <router-outlet/>), menu.ts, service/layout.service.ts
    ├── pages/                      # feature pages, each with <name>.ts/html/scss/spec.ts (+ *.router.ts when lazy)
    │   ├── home/  empty/
    │   ├── config/                 # shell (tabs) + ho-so-van-ban/ + chung-thu-so/ + template-chu-ky/
    │   │                           #   (tbl-action/, template-config/ = template editor)
    │   ├── dong-goi/               # shell + check/ (1 hồ sơ) + multi/ (parent folder)
    │   ├── boc-tach/               # unpack
    │   └── ky-so/                  # single page (folder + template + cert) + sign-step/
    │                               #   (sign-dialog, sign-result-dialog)
    ├── services/                   # config, check, multi-check, pack, multi-pack, unpack, sign, sign-template,
    │                               #   sign-cert-state (verified cert, RAM only), desktop
    ├── models/                     # config, check, multi-check, pack, multi-pack, unpack, sign, sign-template
    └── shared/                     # Shared across the app — see part 2
        ├── import.shared.ts        # SharedImports: Angular + PrimeNG modules to spread into imports
        ├── utils.ts                # class Utils (static helpers)
        ├── app-route-reuse.strategy.ts     # keeps state of routes flagged data: { reuse: true }
        ├── components/             # base/, breadcrumb/, data-table/, file-tree/
        ├── constants/              # data-table.constants.ts, sign.constants.ts
        ├── directives/             # [appEnterKey], [appShiftEnterKey]
        └── models/                 # data-table, environment, request-paging.base
```

Routing: `''` = `Home` (eager); `cau-hinh` (children `ho-so-van-ban`, `chung-thu-so`, `template-chu-ky`,
`template-chu-ky/:id` — `moi` = new), `dong-goi` (children `check` + `multi`), `boc-tach`, `ky-so` are lazy
via `<feature>.router.ts`. `AppRouteReuseStrategy` only keeps state for routes flagged `data: { reuse: true }` —
that gate is **mandatory**, because a lazy parent and its `''` leaf otherwise produce the same key and the router
recurses into `Maximum call stack`.

## 4. Status

- ✅ `src/` bootstrap, `app.routes.ts`, providers in `app.config.ts`.
- ✅ `layout/` (Layout + Topbar + Menu + Footer + LayoutService), `environments/`.
- ✅ `shared/` (BaseComponent, data-table, breadcrumb, file-tree, route-reuse strategy, directives, models,
  constants, Utils, SharedImports).
- ✅ `pages/`, `services/`, `models/` — all five areas built: home, `/cau-hinh` (Hồ sơ/Văn bản · Chứng thư số ·
  Template chữ ký), `/dong-goi` (check + multi), `/ky-so` (folder + template + cert → sign-step), `/boc-tach`.
- ✅ Signing stamps the template's seal / wet-signature images (`kind = 1/2`) as well as the signature block.
- ⚠️ `ng test` is currently red (15/18) on pre-existing TestBed DI errors — the practical gate is `npm run build`.

> **Next:** [shared-and-conventions.md](shared-and-conventions.md) — the `shared/` API and the coding rules every page follows.
