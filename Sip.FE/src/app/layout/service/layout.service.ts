import { Injectable, signal } from '@angular/core';

interface LayoutState {
  /** Desktop: true = ẩn sidebar (content giãn full). */
  staticMenuDesktopInactive: boolean;
  /** Mobile: true = sidebar đang mở dạng overlay. */
  staticMenuMobileActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class LayoutService {
  readonly state = signal<LayoutState>({
    staticMenuDesktopInactive: false,
    staticMenuMobileActive: false
  });

  isDesktop(): boolean {
    return window.innerWidth > 991;
  }

  /** Nút hamburger: desktop -> ẩn/hiện sidebar; mobile -> mở/đóng overlay. */
  onMenuToggle(): void {
    if (this.isDesktop()) {
      this.state.update((s) => ({ ...s, staticMenuDesktopInactive: !s.staticMenuDesktopInactive }));
    } else {
      this.state.update((s) => ({ ...s, staticMenuMobileActive: !s.staticMenuMobileActive }));
    }
  }

  /** Đóng menu mobile (khi click mask hoặc điều hướng). */
  hideMenu(): void {
    this.state.update((s) => ({ ...s, staticMenuMobileActive: false }));
  }
}
