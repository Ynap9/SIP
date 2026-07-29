# Coding Conventions (FE)

## Angular style
- **Standalone components only** — no NgModules. Declare deps in the component's `imports` (spread `...SharedImports`).
- **Signals** for state: `signal()`, `input()`/`input.required()`, `computed()`. Prefer signals over `@Input()`/mutable fields for view state.
- Dependencies via `inject()` inside the class body, not constructor params.
- Root services: `@Injectable({ providedIn: 'root' })`.
- Use `takeUntilDestroyed()` for long-lived subscriptions (router events, streams).

## File naming (no `.component`/`.service` suffixes)
Angular 21 flat style: `config.ts`, `config.html`, `config.scss`, `config.router.ts`, `config.spec.ts`; services `config.service.ts`; models `config.models.ts`. Class names are PascalCase without a `Component` suffix (`Config`, `Layout`, `DataTable`). Selectors are `app-*`.

## Styling
- **Tailwind v4** utilities for layout/spacing (global `src/styles.css`).
- Component styles: `.scss` for pages & shared components, `.css` for layout.
- PrimeNG theme = Aura; keep light mode default (`darkModeSelector: '.app-dark'`).
- UI rules in [`../docs/ui-guidelines.md`](../docs/ui-guidelines.md) — follow it for spacing/components.

## API & response handling
- Call BE only via feature services; prefix URLs with `desktop.apiBase`.
- Type responses as `IBaseResponse(WithData)` matching the BE `ApiResponse` envelope.
- Gate on `BaseComponent.isResponseSucceed(res)` (checks `status === 1`) before using `res.data`; it auto-toasts errors.
- Keep `models/*.models.ts` interfaces in sync with BE DTOs — JSON is **camelCase**.
- **`models/*.models.ts` holds interfaces ONLY.** Enums, label maps and literal constants go in `shared/constants/<feature>.constants.ts` — all constants live under `shared/constants/`, feature ones included. See [`../docs/project-structure.md`](../docs/project-structure.md) §11.

## Pages
- Extend `BaseComponent`; override `ngOnInit()` to load, `onRouteActivated()` to reload on back-nav.
- Lazy-load features via `<feature>.router.ts` + `loadChildren`.
- Reuse `<app-breadcrumb>` and `<app-data-table>`; don't rebuild tables.
- Follow the recipes in [`../docs/skills/`](../docs/skills/) (`feature-page`, `dialog-action`) and plans in [`../docs/plans/`](../docs/plans/).

## Desktop-awareness
- Guard native actions behind `DesktopService.isDesktop`; always provide a browser fallback (paste path / clipboard) so `ng serve` works.
- Normalize user-typed Windows paths with `Utils.normalizeFolderPath` before sending to BE.

## Don'ts
- Don't introduce NgModules or `constructor(private x: ...)` DI.
- Don't hardcode the API host.
- Don't bypass `BaseComponent` toast/confirm helpers with raw PrimeNG calls in pages.
