# Sip.FE Project Structure

> Canonical doc for humans & agents. **Read this before creating new code** to follow the project's real structure and conventions.

## 1. Stack & libraries

- **Angular 21** — standalone components (NO NgModules), **signals** (`signal`, `input`, `computed`).
- **PrimeNG 21** — primary UI kit (table, dialog, toast, button, form controls...). Barrel-exported in `shared/import.shared.ts`.
- **Tailwind CSS v4** — `@import "tailwindcss";` in `src/styles.css`. Use utility classes for layout/spacing.
- **@ng-icons/heroicons** — icon set (declared in `src/config/icons.ts`).
- **moment** — date formatting (used by `Utils`).
- Component styles: `.scss` for shared components (e.g. `data-table.scss`), `.css` for layout. Global = Tailwind in `src/styles.css`.
- Final target: **desktop app** (Electron/Tauri) — see section 8.

## 2. Bootstrap & root

- `src/main.ts` → `bootstrapApplication(App, appConfig)`.
- **Root app files live directly in `src/`** (NOT `src/app/`):
  - `src/app.ts` — `App` (selector `app-root`), contains only `<router-outlet />`.
  - `src/app.html`, `src/app.css`, `src/app.spec.ts`.
  - `src/app.config.ts` — `appConfig` (providers).
  - `src/app.routes.ts` — routes; route `''` = `Layout` (shell) with `children` awaiting pages.
- `src/app/` holds feature & shared code (`layout/`, `shared/`).

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
    ├── layout/                     # Shell (DONE)
    │   ├── layout.ts/html/css      # app-layout: sidebar + topbar + <router-outlet/>
    │   └── sidebar.ts/html/css     # app-sidebar: logo "Đóng gói SIP" + menu
    └── shared/                     # Shared across the app
        ├── import.shared.ts        # SharedImports: Angular + PrimeNG modules to spread into imports
        ├── utils.ts                # class Utils (static helpers)
        ├── components/
        │   ├── base/base-component.ts      # abstract BaseComponent (pages extend this)
        │   ├── breadcrumb/                 # app-breadcrumb (PrimeNG breadcrumb)
        │   └── data-table/                 # app-data-table (rich PrimeNG table)
        ├── constants/
        │   └── data-table.constants.ts     # class CellViewTypes
        ├── directives/
        │   ├── enter-key.directive.ts      # [appEnterKey]
        │   └── shift-enter-key.directive.ts# [appShiftEnterKey]
        └── models/
            ├── data-table.models.ts        # IColumn, ICustomEmit
            ├── environment.models.ts       # IEnvironment
            └── request-paging.base.models.ts # IBaseResponse, IBaseRequestPaging...
```

## 4. `shared/` details

| Item | Symbol | Role |
|---|---|---|
| `import.shared.ts` | `SharedImports` | Array of modules (CommonModule, Forms, + PrimeNG: Button, InputText, Dialog, Toast, Card, Select, MultiSelect, DatePicker, Tooltip...). Spread into `imports: [...SharedImports]`. |
| `utils.ts` | `Utils` | Static helpers: local/session storage, `transformMoney`/`transformPercent` (vi-VN), `convertVietnameseToEng`, `formatDateCallApi`/`formatDateView` (moment), `makeRandom`, `convertParamUrl`... |
| `components/base/base-component.ts` | `BaseComponent` (abstract) | **Pages should extend this.** Injects `Router`, `ActivatedRoute`, `MessageService`, `DialogService`, `ConfirmationService`. Provides: form validation helpers (`isFormInvalid`, `getError`), toast (`messageSuccess/Warning/Error`), confirm (`confirmDelete`, `confirmAction`), `isResponseSucceed(res)` (checks `res.status === 1`), `onRouteActivated()` hook (reload data on navigating back). |
| `components/data-table/` | `app-data-table` (`DataTable`) | PrimeNG table. Inputs: `columns: IColumn[]`, `data`, `pageSize`, `pageNumber`, `total`, `loading`, `tableKey`. Outputs: `onPageChanged`, `customEmit`, `onCustomComp`, `onError`. Column resize (cached in localStorage), many cell types. NOTE: it renders a bottom paginator block — hide/bypass it when "show all / no paging" is required. |
| `components/breadcrumb/` | `app-breadcrumb` (`Breadcrumb`) | Inputs `items: MenuItem[]`, `home: MenuItem`. |
| `constants/data-table.constants.ts` | `CellViewTypes` | Cell render types: `INDEX, DATE, CURRENCY, CHECKBOX, CUSTOM_COMP, LINK_BLANK, STATUS, SECRET, BOOL_CHECK`. |
| `directives/` | `[appEnterKey]`, `[appShiftEnterKey]` | Catch Enter / Shift+Enter, emit event. |
| `models/data-table.models.ts` | `IColumn`, `ICustomEmit` | Column config (`header`, `field`, `cellViewType`, `statusSeverityFunction`...). |
| `models/request-paging.base.models.ts` | `IBaseResponse`, `IBaseRequestPaging`, `IBaseResponseWithData<T>`, `IBaseResponsePaging<T>` | **Standard envelope** & paging request. |
| `models/environment.models.ts` | `IEnvironment` | Type for environment files (`production`, `baseUrl`). |

## 5. Code conventions

- **File names have NO `.component` suffix**: `xxx.ts` / `xxx.html` / `xxx.scss`. Selector prefix `app-`.
- Standalone: declare `imports` in `@Component`; reuse `...SharedImports` from `shared/import.shared.ts`.
- **Pages** should `extends BaseComponent` for toast/confirm/validate/envelope helpers.
- State via `signal()`/`input()`/`computed()`; templates use `@if`/`@for`/`@switch`.
- Inject via `inject()`.
- Format money/date/string: prefer `Utils`.
- Lists: prefer `app-data-table` + `IColumn` over hand-rolled `<table>`.

## 6. Providers to wire when building features

`app.config.ts` currently only has `provideBrowserGlobalErrorListeners()` + `provideRouter(routes)`. When using the following, ADD providers:
- API calls: `provideHttpClient(withFetch())`.
- PrimeNG: `providePrimeNG({ theme: ... })` + animations.
- `BaseComponent` needs `MessageService`, `ConfirmationService`, `DialogService` provided (plus `<p-toast />`, `<p-confirmdialog />` in layout) for toast/confirm.
- Icons: `provideIcons(heroIcons)` (from `src/config/icons.ts`) + use `NgIcon`.

## 7. Back-end (Sip.BE) communication

- **Base URL**: from `environment.baseUrl` (currently `http://localhost:5145`). Never hardcode.
- **Envelope** (every REST returns HTTP 200; read `status`/`code` in body):
  ```ts
  // shared/models/request-paging.base.models.ts
  type IBaseResponse = { status: number; code: number; message: string }; // status: 1 = Success, 0 = Error
  type IBaseResponseWithData<T> = IBaseResponse & { data: T };
  ```
  Use `BaseComponent.isResponseSucceed(res)` (`res.status === 1`).
- **REST**: `HttpClient` + `environment.baseUrl`. Services live in `services/` (section 9), `providedIn: 'root'`.
- **SSE (realtime folder check)**: `POST {baseUrl}/api/core/document/check/ho-so-folder` returns `text/event-stream`. Since it is POST, read via **`fetch` + ReadableStream**, parse each `data: {json}` line → `CheckProgressDto` (`stage`, `targetType`, `target`, `status`, `code`, `message`, `completed`) to tick the UI.

### BE endpoints
- `POST/GET api/core/document/config/ho-so` | `van-ban` — import/list Excel config.
- `POST api/core/document/check/ho-so-folder` — check one record folder (SSE).

## 8. Desktop app notes (Electron/Tauri)

- Pick folders/files via the shell's **native dialog**; the check flow operates on a **local folder path** (Windows path), not web upload.
- No SSR/SEO/deep-link assumptions; the app runs from a local origin inside a webview.
- Offline-first: bundle fonts/icons/assets locally; avoid internet-dependent CDNs.
- The .NET BE runs as a background process (localhost:5145) spawned by the shell; FE calls over HTTP/SSE. Enable CORS for the webview origin; consider disabling HTTPS-redirect on localhost.

## 9. Where new code goes

- **Route pages** → `src/app/pages/<page-name>/` (standalone, `extends BaseComponent`), declared in `app.routes.ts` under `Layout`'s `children`.
- **API services** → `src/app/services/` (`providedIn: 'root'`, `HttpClient`/`fetch`-SSE + `environment.baseUrl`).
- **Feature DTOs** (matching BE) → `src/app/models/`. (Shared/base models already in `shared/models/`.)
- **Reusable components** → `src/app/shared/components/`.

## 10. Status

- ✅ `src/` bootstrap, `app.routes.ts` (route `''` = `Layout`).
- ✅ `layout/` (Layout + Sidebar).
- ✅ `shared/` (BaseComponent, data-table, breadcrumb, directives, models, constants, Utils, SharedImports).
- ✅ `config/icons.ts`, `environments/`.
- ⬜ `pages/`, `services/`, `models/` (feature) — created as screens are built.
- ⬜ Providers HttpClient / PrimeNG theme / MessageService... in `app.config.ts` — add when needed (section 6).
