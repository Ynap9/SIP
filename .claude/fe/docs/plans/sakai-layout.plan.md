# Layout kiểu Sakai (menu co giãn mượt) — Build Plan

> Đọc `docs/sakai-layout.md` (cơ chế) + `docs/project-structure.md` trước. Refactor riêng phần `layout/`; KHÔNG đụng `pages/*` nội dung.

## Requirements
- Chia layout theo Sakai trong `src/app/layout/`: `layout.ts` (wrapper) + `topbar.ts` + `sidebar.ts` + `menu.ts` + `footer.ts` + `service/layout.service.ts`. Đặt tên KHÔNG hậu tố `.component`, selector `app-*` (theo convention Sip).
- **Nút hamburger 3 gạch** (`pi pi-bars`, class `layout-menu-button`) **cạnh logo** trong topbar → `layoutService.onMenuToggle()` ẩn/hiện menu.
- Menu ↔ pages **trượt mượt** như Sakai: desktop ẩn sidebar + content giãn full (`margin-left 22rem ↔ 0`, transition 0.4s); mobile sidebar overlay + `layout-mask` (click ngoài/điều hướng → đóng).
- **Footer** (mọi trang, trong layout): `.layout-footer` → `SIPPACK by` + `<span class="font-bold text-blue-600">SaoMaiSolutions</span>` (học từ app.footer SMS, công ty in màu xanh đậm).
- Giữ ràng buộc Sip: **luôn light** (bỏ dark mode/configurator/overlay/profile của Sakai), Times New Roman, nội dung full-width không bọc box. 3 mục menu giữ nguyên: Trang chủ `/`, Kiểm tra & đóng gói `/dong-goi`, Cấu hình `/cau-hinh`.

## Decisions
- **D1 — Chỉ giữ `menuMode='static'` + mobile**; bỏ overlay/dark/config. `LayoutService` rút gọn: `layoutState = signal({ staticMenuDesktopInactive, staticMenuMobileActive })`, `onMenuToggle()`, `isDesktop()` (`innerWidth>991`). Lý do: Sip không cần đa theme/menu mode.
- **D2 — CSS layout để ở 1 file CSS thuần** `src/styles.css` (thêm block `.layout-*`) HOẶC `src/app/layout/layout.css` global, KHÔNG dựng pipeline SCSS. Lý do: Sip dùng Tailwind v4/CSS; port `_main`/`_responsive` sang CSS thuần với giá trị cố định (light-only), bỏ CSS vars dark.
- **D3 — Menu phẳng đơn giản** (3 item, `RouterLink`+`routerLinkActive`), CHƯA dựng `menuitem` đệ quy + animation. Lý do: Sip chỉ 3 mục, không submenu; giữ gọn, mở rộng sau nếu cần.
- **D4 — Topbar bỏ tiêu đề trang**, theo Sakai chỉ còn hamburger + logo (chữ "SIPPACK"). Bỏ `pageTitle`/`updateTitle` ở layout cũ (hết luôn lỗi snapshot).

## Steps
1. `service/layout.service.ts` — signals + `onMenuToggle()` + `isDesktop()`. Build.
2. CSS layout (`.layout-wrapper/.layout-topbar/.layout-sidebar/.layout-main-container/.layout-static-inactive/.layout-mobile-active/.layout-mask` + transition) vào `src/styles.css`. Build.
3. `topbar.ts` (`app-topbar`): logo "SIPPACK" + nút hamburger → `onMenuToggle()`. `.spec.ts`. Build.
4. `menu.ts` (`app-menu`) + `sidebar.ts` (`app-sidebar` bọc menu): `ul.layout-menu` 3 item. `.spec.ts`. Build. (gỡ/đè `sidebar` cũ.)
5. `footer.ts` (`app-footer`): `.layout-footer` "SIPPACK by SaoMaiSolutions" (công ty xanh đậm). `.spec.ts`. Build.
6. `layout.ts`/`layout.html` viết lại: `.layout-wrapper [ngClass]="containerClass"` → topbar/sidebar/`.layout-main-container`(`.layout-main`+`<router-outlet>`+`<app-footer>`)+`.layout-mask`; outside-click + đóng khi `NavigationEnd`. Giữ `<p-toast>`/`<p-confirmdialog>`. Build.
7. Kiểm thử tay: desktop bấm hamburger → sidebar trượt ẩn/hiện, content giãn mượt; mobile → overlay + mask; footer hiện mọi trang; 3 route hoạt động. `npm run build` cuối cùng pass.

## Expected output
- New: `layout/service/layout.service.ts`, `layout/topbar.ts`, `layout/menu.ts`, `layout/footer.ts` (+ `.spec.ts`), block CSS `.layout-*` trong `src/styles.css`.
- Rewritten: `layout/layout.ts` + `layout/layout.html`, `layout/sidebar.ts` (bọc `app-menu`).
- Untouched: `pages/*`, `services/*`, `models/*`, `app.routes.ts`, `app.config.ts`.
