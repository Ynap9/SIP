# Skill: Collapsible Sakai-style Layout (Sip.FE)

Tái sử dụng để dựng/chỉnh layout menu↔pages co giãn mượt (kiểu Sakai), light-only. Đọc `docs/sakai-layout.md` + `docs/project-structure.md` trước.

## Khi nào dùng
- Cần sidebar trượt ẩn/hiện bằng nút hamburger, content giãn mượt; footer cố định mọi trang.
- KHÔNG dùng cho dark mode / configurator / overlay menu (đã lược bỏ cho Sip).

## Cấu trúc (thư mục `src/app/layout/`)
- `service/layout.service.ts` — `@Injectable({providedIn:'root'})`, state bằng signal.
- `topbar.ts` (`app-topbar`) — hamburger + logo.
- `sidebar.ts` (`app-sidebar`) → bọc `menu.ts` (`app-menu`).
- `footer.ts` (`app-footer`) — `.layout-footer`.
- `layout.ts`/`layout.html` (`app-layout`) — wrapper + `containerClass`.
- File CSS `.layout-*` đặt trong `src/styles.css` (Tailwind v4, không cần SCSS).

## LayoutService (tối giản)
```ts
@Injectable({ providedIn: 'root' })
export class LayoutService {
  readonly state = signal({ staticMenuDesktopInactive: false, staticMenuMobileActive: false });
  isDesktop = () => window.innerWidth > 991;
  onMenuToggle() {
    if (this.isDesktop())
      this.state.update(s => ({ ...s, staticMenuDesktopInactive: !s.staticMenuDesktopInactive }));
    else
      this.state.update(s => ({ ...s, staticMenuMobileActive: !s.staticMenuMobileActive }));
  }
  hideMenu() { this.state.update(s => ({ ...s, staticMenuMobileActive: false })); }
}
```

## containerClass (trên `.layout-wrapper`)
```ts
get containerClass() {
  return {
    'layout-static': true,
    'layout-static-inactive': this.layout.state().staticMenuDesktopInactive,
    'layout-mobile-active': this.layout.state().staticMenuMobileActive,
  };
}
```
- Đóng menu khi `NavigationEnd` và khi click ngoài (so với `.layout-sidebar` + `.layout-menu-button`).

## Hamburger (topbar)
```html
<div class="layout-topbar">
  <div class="layout-topbar-logo-container">
    <button class="layout-menu-button" (click)="layout.onMenuToggle()" aria-label="Menu">
      <i class="pi pi-bars"></i>
    </button>
    <a class="layout-topbar-logo" routerLink="/"><span>SIPPACK</span></a>
  </div>
</div>
```

## CSS cốt lõi (cơ chế mượt — đặt trong `src/styles.css`)
```css
.layout-sidebar{ position:fixed; width:20rem; top:0; left:0; height:100vh;
  transition:transform .4s cubic-bezier(.05,.74,.2,.99); z-index:999; }
.layout-main-container{ min-height:100vh; transition:margin-left .3s; }
@media (min-width:992px){
  .layout-static .layout-main-container{ margin-left:20rem; }
  .layout-static.layout-static-inactive .layout-sidebar{ transform:translateX(-100%); }
  .layout-static.layout-static-inactive .layout-main-container{ margin-left:0; }
  .layout-mask{ display:none; }
}
@media (max-width:991px){
  .layout-sidebar{ transform:translateX(-100%); }
  .layout-main-container{ margin-left:0; }
  .layout-mobile-active .layout-sidebar{ transform:translateX(0); }
  .layout-mask{ display:none; position:fixed; inset:0; z-index:998; background:rgba(0,0,0,.4); }
  .layout-mobile-active .layout-mask{ display:block; }
}
```
- Breakpoint chốt = **992px** (desktop) / 991px (mobile), khớp Sakai.
- `width` sidebar và `margin-left` content phải BẰNG NHAU (vd 20rem) để khít.

## Footer (mọi trang, công ty in màu)
```html
<div class="layout-footer text-base font-medium">
  SIPPACK by <span class="font-bold text-blue-600">SaoMaiSolutions</span>
</div>
```

## Lưu ý
- Sip luôn light → KHÔNG thêm class `app-dark`, không CSS var dark.
- Mỗi component mới có `.spec.ts` smoke (`should create`).
- Kết thúc mỗi bước bằng `npm run build`.
- Tham chiếu `feature-page.skill.md` cho convention chung (standalone, signals, `@if/@for`).
