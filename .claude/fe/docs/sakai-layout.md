# Sakai-style Layout — Reference (học từ `SmsMarketing/sms-marketing.fe`)

> Nguồn: theme Sakai-ng (PrimeNG). Tài liệu này ghi lại **cơ chế** layout menu/pages co giãn "mềm mại" để áp dụng vào Sip.FE. Đọc trước khi làm `sakai-layout.plan.md`.

## 1. Chia component (thư mục `layout/`)
| Sakai (gốc) | Vai trò |
|---|---|
| `app.layout.ts` | Wrapper `.layout-wrapper [ngClass]="containerClass"` → `<app-topbar>`, `<app-sidebar>`, `.layout-main-container` (`.layout-main` + `<router-outlet>` + `<app-footer>`), `.layout-mask`. Lắng nghe outside-click + đóng menu khi `NavigationEnd`. |
| `app.topbar.ts` | `.layout-topbar` → `.layout-topbar-logo-container`: **nút hamburger** `.layout-menu-button` (`pi pi-bars`, `(click)="layoutService.onMenuToggle()"`) + logo. Bên phải: actions (dark mode/config/profile — Sip KHÔNG cần). |
| `app.sidebar.ts` | `.layout-sidebar` bọc `<app-menu>`. |
| `app.menu.ts` | `ul.layout-menu` lặp `model: IAppMenuItem[]` render `li app-menuitem`. |
| `app.menuitem.ts` | Item đệ quy (submenu) + animation `@children` (collapsed↔expanded 400ms). Sip menu phẳng → có thể đơn giản hoá. |
| `app.footer.ts` | `.layout-footer` → `"SMS MARKETING by "` + `<span class="text-primary font-bold hover:underline">DOMOCOM</span>`. |
| `service/layout.service.ts` | State + toggle (mục 2). |

## 2. LayoutService (cốt lõi của toggle)
- `layoutState = signal<{ staticMenuDesktopInactive, staticMenuMobileActive, overlayMenuActive, ... }>()`.
- `layoutConfig = signal<{ menuMode: 'static' | 'overlay', darkTheme, ... }>()`.
- `onMenuToggle()`: desktop (`window.innerWidth > 991`) → đảo `staticMenuDesktopInactive`; mobile → đảo `staticMenuMobileActive`.
- `isDesktop()` = `innerWidth > 991`.

## 3. containerClass (app.layout) → class trên `.layout-wrapper`
- `layout-static` khi `menuMode==='static'`.
- `layout-static-inactive` khi `staticMenuDesktopInactive && menuMode==='static'` → **ẩn sidebar trên desktop**.
- `layout-mobile-active` khi `staticMenuMobileActive` → hiện sidebar dạng overlay trên mobile.
- (`layout-overlay`, `layout-overlay-active` — chế độ overlay, Sip không dùng.)

## 4. CSS tạo hiệu ứng mượt (`assets/layout/_main.scss` + `_responsive.scss`)
- `.layout-main-container { padding: 6rem 2rem 0; transition: margin-left var(--layout-section-transition-duration); }`.
- **Desktop ≥992px, `.layout-static`**: `.layout-main-container { margin-left: 22rem; }`. Khi thêm `.layout-static-inactive`: `.layout-sidebar { transform: translateX(-100%); }` + `.layout-main-container { margin-left: 0; }`.
- `.layout-sidebar`: fixed, `transition: transform .4s cubic-bezier(.05,.74,.2,.99)`.
- **Mobile ≤991px**: sidebar mặc định `translateX(-100%)`; `.layout-mobile-active .layout-sidebar { transform: translateX(0) }` + hiện `.layout-mask` (nền mờ, click để đóng).
- Outside-click (app.layout) dùng `.layout-sidebar` + `.layout-menu-button` để biết click ngoài → đóng menu; `NavigationEnd` cũng đóng.

## 5. Wiring style
- Entry `src/assets/styles.scss` → `@use './layout/layout.scss'` → `@use` các partial (`_main, _topbar, _menu, _footer, _responsive, variables/*`).
- Dùng CSS vars: `--layout-section-transition-duration`, `--surface-border`, `--maskbg` (định nghĩa trong `variables/`).

## 6. Khác biệt với Sip.FE hiện tại (cần xử lý ở plan)
- Sip.FE: Tailwind v4 trong `src/styles.css` (KHÔNG SCSS), layout flex đơn giản (sidebar `w-64` + main), **không** có LayoutService, **luôn light**, font Times New Roman, nội dung full-width không bọc box.
- Cần: port cơ chế trên nhưng **bỏ** dark mode/config/overlay/profile; chỉ giữ `static` + mobile; CSS layout để ở 1 file CSS thuần (Tailwind không cần SCSS).
