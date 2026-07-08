# Sip.FE UI Guidelines

> Rules every page/component must follow. Agents: read this before building or styling UI.

## Layout
- The right content area renders pages **full width** — do NOT wrap routed content in a centered `max-w-*` "box"/card.
  - The shell `layout.html` puts `<router-outlet />` directly inside a full-width `<main class="flex-1 overflow-y-auto bg-white p-6">`.
  - Pages own their internal spacing; start the page root with a simple full-width container (e.g. `<div class="flex flex-col gap-4">`), not a fixed-width card.
- Sidebar is fixed `w-64`; content fills the remaining width and full height.

## Theme & colors
- PrimeNG (Aura preset) is the UI kit. **Dark mode is disabled** via `darkModeSelector: '.app-dark'` in `app.config.ts` → the app is **always light**. Never rely on OS/system dark mode.
- If PrimeNG components ever render dark/black, check that `darkModeSelector` is set to a class that is never applied (i.e. dark mode stays off).
- Use PrimeNG components for forms/tables/dialogs; use Tailwind utilities for layout/spacing only.

## Typography
- App-wide font is **Times New Roman** (`'Times New Roman', Times, serif`), set in `src/styles.css` on `html, body` and forced on `.p-component` (PrimeNG) with `!important`. Do not override the font per component.

## Components
- Lists/tables → `app-data-table` (`IColumn` + `CellViewTypes`). For "show all / no paging" pass `[hidePaginator]="true"` and `[pageSize]="rows().length"`.
- Page header → `app-breadcrumb` (`home` + `items`).
- Pages extend `BaseComponent` (toast/confirm/validate/`isResponseSucceed`).

See also `docs/project-structure.md` and `docs/skills/feature-page.skill.md`.
