import { Component, model, output } from '@angular/core';

import { SharedImports } from '../../../../shared/import.shared';
import { BaseComponent } from '../../../../shared/components/base/base-component';

@Component({
  selector: 'app-config-confirm-dialog',
  imports: [...SharedImports],
  templateUrl: './config-confirm-dialog.html',
  styleUrl: './config-confirm-dialog.scss'
})
export class ConfigConfirmDialog extends BaseComponent {
  // Two-way hiển thị popup với cha.
  visible = model<boolean>(false);

  // Chỉ hỏi xác nhận, KHÔNG liệt kê lại cấu hình: mọi thông tin đó đã nằm sẵn trên màn hình cấu hình ngay
  // phía sau dialog, nhắc lại chỉ làm dialog dài ra chứ không giúp người dùng quyết định.

  /** Bấm "Lưu" -> cha chốt cấu hình và sang bước ký. */
  save = output<void>();

  onCancel(): void {
    this.visible.set(false);
  }

  onSave(): void {
    this.save.emit();
    this.visible.set(false);
  }
}
