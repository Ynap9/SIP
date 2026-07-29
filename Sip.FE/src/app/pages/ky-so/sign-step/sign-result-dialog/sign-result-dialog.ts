import { Component, computed, input, model, output } from '@angular/core';

import { SharedImports } from '../../../../shared/import.shared';
import { BaseComponent } from '../../../../shared/components/base/base-component';
import { ISignResult } from '../../../../models/sign.models';

// Tông màu/nội dung hero suy ra từ kết quả lô ký (dùng trong template thay vì nhồi logic vào HTML).
type SignResultTone = 'success' | 'warn' | 'error';
interface ISignResultStatus {
  tone: SignResultTone;
  /** Class icon PrimeNG hiển thị giữa vòng tròn hero. */
  icon: string;
  title: string;
  subtitle: string;
}

@Component({
  selector: 'app-sign-result-dialog',
  imports: [...SharedImports],
  templateUrl: './sign-result-dialog.html',
  styleUrl: './sign-result-dialog.scss'
})
export class SignResultDialog extends BaseComponent {
  // Two-way hiển thị popup với cha.
  visible = model<boolean>(false);
  // Kết quả cả lô ký (3 trạng thái: đã ký / bỏ qua / lỗi) + thư mục nguồn chứa file đã ký.
  result = input<ISignResult | null>(null);
  // Bấm "Mở thư mục" -> để cha mở (desktop: Explorer; web-dev: copy clipboard).
  openFolder = output<string>();

  // Ưu tiên trạng thái xấu nhất để hero phản ánh đúng: có LỖI -> đỏ; chỉ có BỎ QUA -> vàng; sạch -> xanh.
  // Không hô "thành công" đơn thuần khi thực tế có file lỗi.
  status = computed<ISignResultStatus>(() => {
    const r = this.result();
    const failed = r?.failedCount ?? 0;
    const skipped = r?.skippedCount ?? 0;

    if (failed > 0) {
      return {
        tone: 'error',
        icon: 'pi-exclamation-triangle',
        title: 'Ký số hoàn tất — có lỗi',
        subtitle: `${failed} file gặp lỗi khi ký, cần kiểm tra và ký lại.`
      };
    }
    if (skipped > 0) {
      return {
        tone: 'warn',
        icon: 'pi-check',
        title: 'Ký số hoàn tất',
        subtitle: `${skipped} file bị bỏ qua vì không đạt định dạng PDF/A hai lớp.`
      };
    }
    return {
      tone: 'success',
      icon: 'pi-check',
      title: 'Ký số thành công',
      subtitle: 'Tất cả file đã được ký số và tự nghiệm thu thành công.'
    };
  });

  onOpenFolder(): void {
    const folder = this.result()?.outputFolder;
    if (folder) this.openFolder.emit(folder);
  }

  onClose(): void {
    this.visible.set(false);
  }
}
