import { Injectable, isDevMode } from '@angular/core';

// Bề rộng cửa sổ mà giao diện được dựng cho. Hẹp hơn mức này thì KHÔNG xếp lại bố cục mà thu nhỏ đều toàn
// bộ app: mọi khối giữ đúng tỉ lệ và đúng chỗ, chỉ nhỏ đi cùng nhau như nhìn từ xa.
const DESIGN_WIDTH_PX = 1440;

// Sàn thu nhỏ. Cửa sổ app đã bị chặn ở 1000px nên mức này chỉ chạm tới khi chạy trong trình duyệt;
// nhỏ hơn nữa thì chữ không còn đọc được, thà để trang tự cuộn.
const MIN_ZOOM = 0.6;

@Injectable({ providedIn: 'root' })
export class LayoutService {
  constructor() {
    this.applyZoom();
    window.addEventListener('resize', () => this.applyZoom());
    if (!isDevMode()) {
      this.blockBrowserBehaviors();
    }
  }

  private blockBrowserBehaviors(): void {
    document.addEventListener('contextmenu', (event) => {
      const target = event.target as HTMLElement | null;
      if (!target?.closest('input, textarea, [contenteditable="true"]')) {
        event.preventDefault();
      }
    });

    window.addEventListener(
      'wheel',
      (event) => {
        if (event.ctrlKey) event.preventDefault();
      },
      { passive: false }
    );

    document.addEventListener('keydown', (event) => {
      const key = event.key.toLowerCase();
      const withCtrl = event.ctrlKey || event.metaKey;
      if (
        key === 'f5' ||
        (withCtrl && ['r', 'p', 'u', 'f', 'g', 'j', '+', '-', '=', '0'].includes(key)) ||
        (event.altKey && (key === 'arrowleft' || key === 'arrowright'))
      ) {
        event.preventDefault();
      }
    });
  }

  /**
   * Thu nhỏ đều toàn bộ giao diện cho vừa cửa sổ, thay vì bẻ lại bố cục.
   * Chỉ thu chứ không phóng (trần 1): cửa sổ rộng hơn khổ thiết kế thì để nội dung giãn ra tự nhiên,
   * phóng to lên là chữ vỡ và mỗi màn hình một cỡ khác nhau.
   */
  private applyZoom(): void {
    const zoom = Math.min(1, Math.max(window.innerWidth / DESIGN_WIDTH_PX, MIN_ZOOM));
    const root = document.documentElement;
    root.style.setProperty('zoom', String(zoom));

    // 1vh vẫn đo theo cửa sổ THẬT, không theo hệ toạ độ đã zoom. Dùng thẳng 100vh trong vùng đã thu nhỏ là
    // topbar và vùng nội dung chỉ còn cao bằng zoom lần chiều cao cửa sổ, hụt hẳn một khoảng dưới đáy.
    // Chia ngược cho zoom để quy về đúng hệ toạ độ đang vẽ.
    root.style.setProperty('--app-vh', `${window.innerHeight / zoom}px`);
  }
}
