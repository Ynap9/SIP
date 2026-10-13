# Sip.FE UI Guidelines

> Rules every page/component must follow. Agents: read this before building or styling UI.

## Layout
- **Desktop-app look, not web**: no landing-page heroes, gradients, marketing cards, link underlines or
  responsive breakpoints. Compact headers, flat bordered panels (`rounded-md`), `cursor-default` on clickable
  tiles, `draggable="false"` on links.
- The content area renders pages **full width** — do NOT wrap routed content in a centered `max-w-*` "box"/card.
  **Only exception: Home** (`pages/home`) is a start screen, so its content is one block capped at
  `max-w-[1280px]`, centred horizontally and vertically — on wide windows the tiles otherwise stretch thin.
  - `layout.html` puts `<router-outlet />` inside the flush `.layout-main` panel (topbar above, status bar below).
  - Pages own their internal spacing; start the page root with a simple full-width container (e.g. `<div class="flex flex-col gap-4">`), not a fixed-width card.
- There is no sidebar: navigation is an icon + label tab strip in the topbar (see
  [`../architecture/04-layout.md`](../architecture/04-layout.md)).

## Theme & colors
- PrimeNG (Aura preset) is the UI kit. **Dark mode is disabled** via `darkModeSelector: '.app-dark'` in `app.config.ts` → the app is **always light**. Never rely on OS/system dark mode.
- If PrimeNG components ever render dark/black, check that `darkModeSelector` is set to a class that is never applied (i.e. dark mode stays off).
- Use PrimeNG components for forms/tables/dialogs; use Tailwind utilities for layout/spacing only.

## Typography
- App-wide font is **Segoe UI** (system sans-serif stack), 14px, set in `src/styles.css` on `html, body` and forced on `.p-component` (PrimeNG) with `!important`. Do not override the font per component.

## Components
- Lists/tables → `app-data-table` (`IColumn` + `CellViewTypes`). For "show all / no paging" pass `[hidePaginator]="true"` and `[pageSize]="rows().length"`.
- Page header → `app-breadcrumb` (`home` + `items`).
- Pages extend `BaseComponent` (toast/confirm/validate/`isResponseSucceed`).

See also `docs/project-structure.md` and `docs/skills/feature-page.skill.md`.
