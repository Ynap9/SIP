# Sip.FE — `shared/` API & Code Conventions

> **Part 2 of 3** · previous: [project-structure.md](project-structure.md) — stack, bootstrap, folder tree.

## 1. `shared/` details

| Item | Symbol | Role |
|---|---|---|
| `import.shared.ts` | `SharedImports` | Array of modules (CommonModule, Forms, + PrimeNG: Button, InputText, Dialog, Toast, Card, Select, MultiSelect, DatePicker, Tooltip...). Spread into `imports: [...SharedImports]`. **Table / Tag / ProgressBar / Steps / SelectButton are NOT in it** — import those per page. |
| `utils.ts` | `Utils` | Static helpers: local/session storage, `transformMoney`/`transformPercent` (vi-VN), `convertVietnameseToEng`, `formatDateCallApi`/`formatDateView` (moment), `makeRandom`, plus the two that matter daily: **`normalizeFolderPath`** (trim, strip quotes, `\`→`/`, drop trailing slash) and **`compareDocSeq`** (numeric-aware sort so `.2` < `.10`). |
| `components/base/base-component.ts` | `BaseComponent` (abstract) | **Pages should extend this.** Injects `Router`, `ActivatedRoute`, `MessageService`, `DialogService`, `ConfirmationService`. Provides: form validation helpers (`isFormInvalid`, `getError`), toast (`messageSuccess/Warning/Error`), confirm (`confirmDelete`, `confirmAction`), `isResponseSucceed(res)` (checks `res.status === 1`), `onRouteActivated()` hook (reload data on navigating back). |
| `components/data-table/` | `app-data-table` (`DataTable`) | PrimeNG table. Inputs: `columns: IColumn[]`, `data`, `pageSize`, `pageNumber`, `total`, `loading`, `tableKey`, `hidePaginator`, `scrollHeight`. Outputs: `onPageChanged`, `customEmit`, `onCustomComp`, `onError`. Column resize cached in `localStorage`. "Show all / no paging" → `[hidePaginator]="true"` + `[pageSize]="rows().length"`. `scrollHeight="flex"` makes the body scroll inside a parent-sized `.table-frame`. For **realtime SSE** rows use a plain `p-table` instead. |
| `components/breadcrumb/` | `app-breadcrumb` (`Breadcrumb`) | Inputs `items: MenuItem[]`, `home: MenuItem`. |
| `components/file-tree/` | `app-file-tree` (`FileTree`) | `nodes = input<IFileNode[]>()`, `open = output<string>()`. Recursive via `NgTemplateOutlet` — deliberately **not** PrimeNG `p-tree`, which stack-overflowed on real unpack output. |
| `constants/data-table.constants.ts` | `CellViewTypes` | Cell render types: `INDEX, DATE, CURRENCY, CHECKBOX, CUSTOM_COMP, LINK_BLANK, STATUS, SECRET, BOOL_CHECK`. |
| `directives/` | `[appEnterKey]`, `[appShiftEnterKey]` | Catch Enter / Shift+Enter, emit event. |
| `models/data-table.models.ts` | `IColumn`, `ICustomEmit` | Column config (`header`, `field`, `cellViewType`, `statusSeverityFunction`...). |
| `models/request-paging.base.models.ts` | `IBaseResponse`, `IBaseRequestPaging`, `IBaseResponseWithData<T>`, `IBaseResponsePaging<T>` | **Standard envelope** & paging request — see [part 3](backend-integration.md). |
| `models/environment.models.ts` | `IEnvironment` | Type for environment files (`production`, `baseUrl`). |

## 2. Code conventions

- **File names have NO `.component` suffix**: `xxx.ts` / `xxx.html` / `xxx.scss`. Selector prefix `app-`.
- Standalone: declare `imports` in `@Component`; reuse `...SharedImports` from `shared/import.shared.ts`.
- **Pages** should `extends BaseComponent` for toast/confirm/validate/envelope helpers.
- State via `signal()`/`input()`/`computed()`; templates use `@if`/`@for`/`@switch`.
- Inject via `inject()` — never `constructor(private x: …)`. Long-lived subscriptions get `takeUntilDestroyed()`.
- Format money/date/string: prefer `Utils`.
- Lists: prefer `app-data-table` + `IColumn` over hand-rolled `<table>`.
- **Every component has a `<name>.spec.ts`** smoke test (`TestBed` + `it('should create')`); do not stuff providers
  into a spec. The practical build gate is **`npm run build`**.

Full rule list: [`../architecture/05-conventions.md`](../architecture/05-conventions.md).

## 3. Providers (already wired in `app.config.ts`)

`provideRouter(routes)` + the custom `RouteReuseStrategy`, `provideHttpClient(withFetch())`,
`providePrimeNG({ theme: Aura, options: { darkModeSelector: '.app-dark' } })` + animations, `MessageService`,
`ConfirmationService`, `DialogService` (`<p-toast />` / `<p-confirmdialog />` are mounted in `layout/`), plus
`provideBrowserGlobalErrorListeners()`. Reuse them — do not re-provide in a page or a spec.
`provideIcons(heroIcons)` from `src/config/icons.ts` is **not** wired — add it if a page starts using `NgIcon`.

> `darkModeSelector: '.app-dark'` exists to **disable** OS-driven dark mode. Nothing ever adds that class and there
> is no theme state anywhere — the app is light-only by design.

## 4. Where new code goes

- **Route pages** → `src/app/pages/<page-name>/` (standalone, `extends BaseComponent`), declared in `app.routes.ts`
  under `Layout`'s `children`, or in a lazy `<feature>.router.ts`.
- **API services** → `src/app/services/` (`providedIn: 'root'`, `HttpClient`/`fetch`-SSE + `desktop.apiBase`).
- **Feature DTOs** (matching BE) → `src/app/models/<feature>.models.ts` — **interfaces only**.
- **Enums + constants** → `src/app/shared/constants/<feature>.constants.ts` — see section 5.
- **Reusable components** → `src/app/shared/components/`.

## 5. Enums & constants live OUTSIDE models

`models/*.models.ts` holds **interfaces only**. Every enum, label map, severity map, or literal constant goes in
`src/app/shared/constants/<feature>.constants.ts`; the model file imports the enum from there.

**All constants live under `shared/constants/`** — app-wide ones (`data-table.constants.ts`) and feature ones
(`sign.constants.ts`) alike. There is no `src/app/constants/` folder; do not create one.

```ts
// shared/constants/sign.constants.ts
export enum ECertSource { Local = 0, Server = 1, UsbToken = 2 }
export const CERT_SOURCE_LABELS: Record<ECertSource, string> = { ... };

// models/sign.models.ts
import { ECertSource } from '../shared/constants/sign.constants';
export interface ISignCert { source: ECertSource; ... }
```

Why: a model file is the wire contract with the BE DTO and should be diffable against it one-to-one. Display
concerns (Vietnamese labels, tag colours) change for FE reasons alone — mixing them makes it hard to see whether a
model still matches its DTO, and pages end up importing a "model" file just to get a colour.

**Known debt:** `models/check.models.ts` still declares `ETimestampVerdict` inline — move it to
`shared/constants/check.constants.ts` when that file is next touched.

> **Next:** [backend-integration.md](backend-integration.md) — how the FE talks to `Sip.BE`, and the desktop host.
