import { Component, inject, input, model, output, signal } from '@angular/core';

import { SharedImports } from '../../../../shared/import.shared';
import { BaseComponent } from '../../../../shared/components/base/base-component';
import { PackService } from '../../../../services/pack.service';
import { DesktopService } from '../../../../services/desktop.service';
import { IPackRequest, IPackResult } from '../../../../models/pack.models';

@Component({
  selector: 'app-package-dialog',
  imports: [...SharedImports],
  templateUrl: './package-dialog.html',
  styleUrl: './package-dialog.scss'
})
export class PackageDialog extends BaseComponent {
  private packService = inject(PackService);
  protected desktop = inject(DesktopService);

  // Thư mục hồ sơ đã kiểm tra (read-only) + trạng thái hiển thị form (two-way với cha)
  folderPath = input<string>('');
  visible = model<boolean>(false);
  packed = output<IPackResult>();

  // Field nhập (tất cả optional - BE nullable)
  maPhong = signal<string>('');
  nguoiTao = signal<string>('');
  outputPath = signal<string>('');

  packing = signal<boolean>(false);
  showSuccess = signal<boolean>(false);
  result = signal<IPackResult | null>(null);

  onCancel(): void {
    this.visible.set(false);
  }

  onSubmit(): void {
    const req: IPackRequest = {
      folderPath: this.folderPath(),
      outputPath: this.outputPath().trim() || undefined,
      maPhong: this.maPhong().trim() || undefined,
      nguoiTao: this.nguoiTao().trim() || undefined
    };

    this.packing.set(true);
    this.packService.pack(req).subscribe({
      next: (res) => {
        this.packing.set(false);
        if (this.isResponseSucceed(res)) {
          this.result.set(res.data);
          this.visible.set(false);
          this.showSuccess.set(true);
          this.packed.emit(res.data);
        }
      },
      error: () => {
        this.packing.set(false);
        this.messageError('Đóng gói thất bại. Vui lòng thử lại.');
      }
    });
  }

  closeSuccess(): void {
    this.showSuccess.set(false);
  }

  /** Chọn thư mục xuất bằng dialog native (desktop). */
  async chooseOutput(): Promise<void> {
    const path = await this.desktop.pickFolder();
    if (path) {
      this.outputPath.set(path);
    }
  }

  /** Mở thư mục/file kết quả: desktop -> Explorer; web-dev -> copy clipboard. */
  openFolder(path: string): void {
    if (!path) return;
    if (this.desktop.openPath(path)) return;
    navigator.clipboard?.writeText(path).then(
      () => this.messageSuccess('Đã sao chép đường dẫn vào clipboard.'),
      () => this.messageWarning('Không mở được thư mục. Đường dẫn: ' + path)
    );
  }
}
