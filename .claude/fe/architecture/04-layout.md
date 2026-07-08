# Layout Shell (`src/app/layout`)

The app shell that wraps every routed page. Follows the **Sakai** layout (see [`../docs/sakai-layout.md`](../docs/sakai-layout.md)).

## Files
```
layout/
├── layout.ts / layout.html / layout.css   # <app-layout>: shell + <router-outlet/>
├── topbar.ts                               # <app-topbar>: header + hamburger
├── sidebar.ts                              # <app-sidebar>: logo + menu
├── menu.ts                                 # menu model / items
├── footer.ts                               # <app-footer>
└── service/layout.service.ts               # LayoutService (sidebar state)
```

## Layout component
- Standalone; `imports: [CommonModule, RouterOutlet, Topbar, Sidebar, Footer, ToastModule, ConfirmDialogModule]`.
- Renders the shell (topbar + sidebar + footer) around `<router-outlet/>` where pages render.
- Injects `LayoutService`; on `NavigationEnd` closes the mobile menu (`hideMenu()`).
- `containerClass` getter maps `LayoutService.state()` → CSS classes (`layout-static`, `layout-static-inactive`, `layout-mobile-active`).
- `ToastModule` + `ConfirmDialogModule` mounted here so `BaseComponent` toasts/confirms work app-wide (paired with the `MessageService`/`ConfirmationService`/`DialogService` providers in `app.config.ts`).

## LayoutService (`service/layout.service.ts`)
Signal-based sidebar state — the single source of truth for menu open/close:
```ts
state = signal<LayoutState>({ staticMenuDesktopInactive: false, staticMenuMobileActive: false });
isDesktop(): boolean          // window.innerWidth > 991
onMenuToggle(): void          // desktop: collapse/expand; mobile: open/close overlay
hideMenu(): void              // close mobile overlay
```
Desktop = collapse the static sidebar; mobile = toggle an overlay. State is updated immutably via `state.update(...)`.

## Providers (in `src/app.config.ts`, not here)
`appConfig` wires the app: `provideRouter(routes)`, `provideHttpClient(withFetch())`, `provideAnimationsAsync()`, `providePrimeNG({ theme: Aura, darkModeSelector: '.app-dark' })`, plus `MessageService`, `ConfirmationService`, `DialogService`. PrimeNG dark mode is opt-in via the `.app-dark` selector (default is light).
