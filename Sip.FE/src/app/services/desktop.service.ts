import { Injectable, isDevMode } from '@angular/core';
import { environment } from '../../environments/environment';

/**
 * Cầu nối với host desktop (Photino qua window.external).
 * - Desktop: dùng dialog/Explorer native qua web message.
 * - Web-dev (trình duyệt thường): isDesktop=false -> các trang tự fallback (ô dán path / clipboard).
 */
@Injectable({ providedIn: 'root' })
export class DesktopService {
  private external: any = (window as any).external;
  private pendingPickFolder?: (path: string) => void;
  private pendingPickFile?: (path: string) => void;

  constructor() {
    if (this.isDesktop && typeof this.external.receiveMessage === 'function') {
      this.external.receiveMessage((message: string) => this.onMessage(message));
    }
  }

  /** Đang chạy trong host desktop hay không. */
  get isDesktop(): boolean {
    return !!this.external && typeof this.external.sendMessage === 'function';
  }

  /**
   * Base URL gọi API:
   * - Bản build (desktop hoặc web do API phục vụ): same-origin (rỗng) -> gọi đúng cổng host.
   * - `ng serve` (dev): dùng environment.baseUrl (vd http://localhost:5145).
   * Dùng isDevMode() thay vì dò window.external để chắc chắn không phụ thuộc bridge Photino.
   */
  get apiBase(): string {
    return isDevMode() ? environment.baseUrl : '';
  }

  /** Mở dialog chọn thư mục native; trả về đường dẫn (rỗng nếu hủy / không phải desktop). */
  pickFolder(): Promise<string> {
    if (!this.isDesktop) return Promise.resolve('');
    return new Promise<string>((resolve) => {
      this.pendingPickFolder = resolve;
      this.external.sendMessage('pickFolder');
    });
  }

  /** Mở dialog chọn file .zip native; trả về đường dẫn (rỗng nếu hủy / không phải desktop). */
  pickFile(): Promise<string> {
    if (!this.isDesktop) return Promise.resolve('');
    return new Promise<string>((resolve) => {
      this.pendingPickFile = resolve;
      this.external.sendMessage('pickFile');
    });
  }

  /** Mở thư mục/file trong Explorer. Trả về false nếu không phải desktop (để gọi tự fallback). */
  openPath(path: string): boolean {
    if (!this.isDesktop || !path) return false;
    this.external.sendMessage('openPath:' + path);
    return true;
  }

  private onMessage(message: string): void {
    if (typeof message !== 'string') return;
    if (message.startsWith('folder:') && this.pendingPickFolder) {
      const resolve = this.pendingPickFolder;
      this.pendingPickFolder = undefined;
      resolve(message.slice('folder:'.length));
    } else if (message.startsWith('file:') && this.pendingPickFile) {
      const resolve = this.pendingPickFile;
      this.pendingPickFile = undefined;
      resolve(message.slice('file:'.length));
    }
  }
}
