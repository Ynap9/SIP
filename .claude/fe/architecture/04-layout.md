# Layout Shell (`src/app/layout`)

The app shell that wraps every routed page. Follows the **Sakai** layout (see [`../docs/sakai-layout.md`](../docs/sakai-layout.md)).

## Files
```
layout/
├── layout.ts / layout.html / layout.css   # <app-layout>: shell + <router-outlet/>
├── topbar.ts                               # <app-topbar>: logo + divider + <app-menu/>
├── menu.ts                                 # <app-menu>: horizontal nav tab strip
├── footer.ts                               # <app-footer>: desktop status bar (.layout-statusbar)
└── service/layout.service.ts               # LayoutService (window zoom + desktop behaviour)
```

## Desktop-app look (2026-10-13)
The UI must read as a **desktop application, not a web page** (Angular + Photino stay). The shell is:
- **Topbar** (`--layout-topbar-height` 3rem, grey chrome `#f3f4f6`): compact `SIPPACK` logo, divider, nav.
- **Content** (`.layout-main`): flush white panel filling the window — **no floating card, no rounded
  corners, no shadow, no grey margin** around it.
- **Status bar** (`--layout-statusbar-height` 1.75rem, fixed bottom): "Sẵn sàng" left, product right.
  It replaced the old web-style footer.
- Global: `body { user-select: none; cursor: default }` (text stays selectable in inputs, table cells and
  `.selectable`), links/images not draggable, thin desktop scrollbars.

## Navigation — top tab strip
There is **no sidebar component**. The 5 route items are icon + label buttons (`.layout-topbar-nav-item`):
6px radius, hover = grey fill, active = white raised tab with border, accent `#4f46e5` text/icon and
`font-weight: 600`. Links use `draggable="false"` and `cursor: default`.

There is no overflow/collapse handling and no mobile drawer: the window has a 1000px minimum and the app
**scales instead of reflowing** (see `LayoutService`), so the 5 tabs always fit. Revisit only past ~7 items.

## Layout component
- Standalone; `imports: [RouterOutlet, Topbar, Footer, ToastModule, ConfirmDialogModule]`.
- Renders the shell (topbar + footer) around `<router-outlet/>` where pages render.
- Injects `LayoutService` **without reading it** — that injection is what constructs the root-provided
  service, and its constructor applies the zoom. Removing it silently disables app-wide scaling.
- `ToastModule` + `ConfirmDialogModule` mounted here so `BaseComponent` toasts/confirms work app-wide (paired with the `MessageService`/`ConfirmationService`/`DialogService` providers in `app.config.ts`).

## LayoutService (`service/layout.service.ts`)
In a build (`!isDevMode()`) it also blocks browser behaviour: right-click menu (except inside inputs),
Ctrl+wheel zoom, F5 / Ctrl+R/P/U/F/G/J/+/-/0, Alt+←/→. `ng serve` keeps them for debugging.
No menu state. On construction and on every `resize` it applies
`zoom = clamp(innerWidth / 1440, 0.6, 1)` to `<html>` and publishes `--app-vh` (`innerHeight / zoom`,
divided back because `vh` is measured in unzoomed coordinates). This is the project's **scale, never
reflow** rule: shrinking the window zooms the whole app instead of rearranging it.

`--layout-topbar-height` (3rem) and `--layout-statusbar-height` (1.75rem) must stay fixed —
`.layout-main-container` height = `--app-vh` minus both.

## Providers (in `src/app.config.ts`, not here)
`appConfig` wires the app: `provideRouter(routes)`, `provideHttpClient(withFetch())`, `provideAnimationsAsync()`, `providePrimeNG({ theme: Aura, darkModeSelector: '.app-dark' })`, plus `MessageService`, `ConfirmationService`, `DialogService`. PrimeNG dark mode is opt-in via the `.app-dark` selector (default is light).
