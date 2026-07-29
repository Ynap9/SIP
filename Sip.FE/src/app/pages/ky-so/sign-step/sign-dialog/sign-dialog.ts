import { Component, model, output, signal } from '@angular/core';

import { SharedImports } from '../../../../shared/import.shared';
import { BaseComponent } from '../../../../shared/components/base/base-component';

@Component({
  selector: 'app-sign-dialog',
  imports: [...SharedImports],
  templateUrl: './sign-dialog.html',
  styleUrl: './sign-dialog.scss'
})
export class SignDialog extends BaseComponent {
  // Two-way hiển thị popup với cha.
  visible = model<boolean>(false);
  // Bấm "Bắt đầu" -> phát thông tin ký để cha chạy SSE ký.
  start = output<{ reason: string; location: string }>();

  // Field nhập (tất cả optional - BE dùng mặc định của SigningConstants nếu để trống).
  reason = signal<string>('');
  location = signal<string>('');

  onCancel(): void {
    this.visible.set(false);
  }

  onStart(): void {
    this.start.emit({ reason: this.reason().trim(), location: this.location().trim() });
    this.visible.set(false);
  }
}
