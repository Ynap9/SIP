# Plan — Top navigation tab strip

> Move primary navigation from the left sidebar into a horizontal tab strip inside the topbar.

## Requirements / Input

- Current layout: fixed topbar (3.5rem) + fixed sidebar card (17rem) + content card, all floating on a
  gray page background. Nav items live in `menu.ts`, rendered by `sidebar.ts`.
- The 5 top-level routes are unchanged: `/`, `/dong-goi`, `/ky-so`, `/boc-tach`, `/cau-hinh`.
- Scope decided by the user: **navigation only**. Keep the current look — gray page background, white
  rounded cards, shadows, current padding, the footer.
- Must keep working: `LayoutService.applyZoom()` (scale, never reflow), `--app-vh`,
  `body { overflow: hidden }`, and the `.table-frame` / `.table-frame-scroll` / `.row-expand-scroll`
  scroll system currently being introduced.
- Pattern reference: WinUI `NavigationView` with `PaneDisplayMode="Top"`.

## Decisions

| Decision | Why |
|---|---|
| `--layout-topbar-height` stays `3.5rem` | `--app-vh` and `.layout-main-container` height derive from it, so the scroll system needs no re-tuning. A 14px label fits 56px easily. |
| Tabs are **text-only** — no icons | User asked for classic tabs. The `icon` field is dropped from `NavItem` and from all 5 entries rather than left unused. The topbar logo keeps its own icon. |
| Delete the mobile drawer (mask, hamburger, `staticMenu*` state) | The window has a 1000px minimum and the app scales instead of reflowing, so 5 tabs always fit. The drawer is unreachable code. |
| Flat text tabs, active marked by a blue underline | User's final call: no box chrome on any item. Raised tab boxes read as a row of buttons, which is what they rejected. The underline uses the existing accent `#4f46e5` so the app keeps a single accent colour. |
| No overflow handling on the strip | Scale-not-reflow: the whole app zooms out, the strip never has to collapse. Revisit only past ~7 items. |

## Steps

1. `menu.ts` — replace the vertical `<nav>` with a horizontal row of full-height, label-only tabs.
   `routerLinkActive` marks the selected tab; keep
   `[routerLinkActiveOptions]="{ exact: item.link === '/' }"`. Drop `icon` from the `NavItem`
   interface and from all 5 `navItems` entries.
2. `topbar.ts` — drop the hamburger button and the `LayoutService` injection; render `<app-menu />`
   after the logo block, separated by a 1px vertical divider.
3. Delete `sidebar.ts` and `sidebar.spec.ts`.
4. `layout.html` — remove `<app-sidebar />` and the `.layout-mask` div; keep `<p-toast />`,
   `<p-confirmdialog />`, `.layout-main-container` and `<app-footer />`.
5. `layout.ts` — remove the `Sidebar` import, the `containerClass` getter and its `[ngClass]` binding,
   and the `NavigationEnd` → `hideMenu()` subscription; drop the now-unused `CommonModule`, `Router`,
   `takeUntilDestroyed` and `filter` imports.
6. `service/layout.service.ts` — keep only `applyZoom()` and its resize listener. Remove `LayoutState`,
   the `state` signal, `isDesktop()`, `onMenuToggle()` and `hideMenu()`.
7. `styles.css` — remove `--layout-sidebar-width`, `.layout-sidebar`, `.layout-mask` and both `@media`
   blocks. Set `.layout-main-container { margin-left: 0 }` and drop its `margin-left` transition. Add
   `.layout-topbar-nav` rules: items are flat text stretched to the topbar height, carrying
   `border-bottom: 2px solid transparent` so the underline slot is reserved and the label never shifts;
   hover only darkens the text; `.is-active` sets `border-bottom-color: #4f46e5`, the same accent colour,
   plus `font-weight: 600`. Leave every other rule alone — the card look stays.
8. Update `layout.spec.ts`, `menu.spec.ts` and `topbar.spec.ts` so they still mount (no `Sidebar`, no
   `LayoutService` menu API).
9. Update [`04-layout.md`](../../architecture/04-layout.md): the topbar now owns navigation, the
   sidebar component no longer exists, and `LayoutService` is zoom-only.
10. `npm run build`.

## Expected output

- One 3.5rem topbar holding the logo plus 5 text-only tabs; the tab for the current screen is accent
  coloured with a 2px accent underline, and no item renders as a box or button.
- The content card spans the full window width; no sidebar, no hamburger, no overlay mask.
- Page background, card radius/shadow, padding and footer look exactly as before.
- Resizing the window still scales the whole UI; tables still scroll inside their frames.
- `npm run build` succeeds.
