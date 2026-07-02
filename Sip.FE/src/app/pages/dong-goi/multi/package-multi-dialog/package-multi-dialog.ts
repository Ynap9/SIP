import { Component, inject, input, model, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { ProgressBarModule } from 'primeng/progressbar';

import { SharedImports } from '../../../../shared/import.shared';
import { BaseComponent } from '../../../../shared/components/base/base-component';
import { MultiPackService } from '../../../../services/multi-pack.service';
import { DesktopService } from '../../../../services/desktop.service';
import { IMultiPackProgress, IMultiPackRequest } from '../../../../models/multi-pack.models';

interface IZipResult {
  hoSoName: string;
  zipPath: string;
}

@Component({
  selector: 'app-package-multi-dialog',
  imports: [...SharedImports, ProgressBarModule],
  templateUrl: './package-multi-dialog.html',
  styleUrl: './package-multi-dialog.scss'
})
export class PackageMultiDialog extends BaseComponent {
  private multiPackService = inject(MultiPackService);
  protected desktop = inject(DesktopService);

  // Thư mục cha + danh sách hồ sơ đã đạt (allowlist) + trạng thái hiển thị (two-way với cha)
  parentFolderPath = input<string>('');
  passedHoSoNames = input<string[]>([]);
  visible = model<boolean>(false);

  // Field nhập (số luồng song song KHÔNG cho nhập -> BE tự tính theo số nhân CPU của máy)
  maPhong = signal<string>('');
  nguoiTao = signal<string>('');
  outputPath = signal<string>('');

  // Tiến trình đóng gói
  packing = signal<boolean>(false);
  percent = signal<number>(0);
  zipResults = signal<IZipResult[]>([]);

  // Kết quả tổng kết
  showSuccess = signal<boolean>(false);
  summary = signal<IMultiPackProgress | null>(null);

  private sub?: Subscription;

  onCancel(): void {
    if (this.packing()) return;
    this.visible.set(false);
  }

  async chooseOutput(): Promise<void> {
    const path = await this.desktop.pickFolder();
    if (path) {
      this.outputPath.set(path);
    }
  }

  onSubmit(): void {
    if (!this.outputPath().trim()) {
      this.messageWarning('Vui lòng chọn thư mục xuất.');
      return;
    }

    const req: IMultiPackRequest = {
      parentFolderPath: this.parentFolderPath(),
      outputPath: this.outputPath().trim(),
      maPhong: this.maPhong().trim() || undefined,
      nguoiTao: this.nguoiTao().trim() || undefined,
      // maxDegreeOfParallelism bỏ trống -> BE tự tính theo số nhân CPU của máy chạy app
      hoSoNames: this.passedHoSoNames()
    };

    this.packing.set(true);
    this.percent.set(0);
    this.zipResults.set([]);
    this.summary.set(null);

    this.sub?.unsubscribe();
    this.sub = this.multiPackService.packMulti(req).subscribe({
      next: (ev) => this.handleEvent(ev),
      error: () => {
        this.packing.set(false);
        this.messageError('Đóng gói thất bại. Vui lòng thử lại.');
      },
      complete: () => this.packing.set(false)
    });
  }

  private handleEvent(ev: IMultiPackProgress): void {
    if (typeof ev.percent === 'number') {
      this.percent.set(ev.percent);
    }

    if (ev.targetType === 'hoso' && ev.status === 1 && ev.zipPath) {
      this.zipResults.update((list) => [...list, { hoSoName: ev.hoSoName, zipPath: ev.zipPath! }]);
    } else if (ev.targetType === 'parent') {
      this.messageError(ev.message);
    }

    if (ev.completed) {
      this.percent.set(100);
      this.summary.set(ev);
      this.visible.set(false);
      this.showSuccess.set(true);
    }
  }

  closeSuccess(): void {
    this.showSuccess.set(false);
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
