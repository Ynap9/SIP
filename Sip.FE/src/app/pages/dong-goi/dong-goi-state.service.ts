import { Injectable, signal } from '@angular/core';

/**
 * Giữ chế độ (một hồ sơ / nhiều hồ sơ) của màn Kiểm tra & đóng gói qua các lần điều hướng,
 * để khi quay lại trang vẫn đúng chế độ đã chọn trước đó.
 */
@Injectable({ providedIn: 'root' })
export class DongGoiStateService {
  readonly mode = signal<'single' | 'multi'>('single');
}
