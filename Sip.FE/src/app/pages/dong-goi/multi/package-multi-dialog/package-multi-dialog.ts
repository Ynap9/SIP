import { Component, computed, inject, input, model, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { ProgressBarModule } from 'primeng/progressbar';

import { SharedImports } from '../../../../shared/import.shared';
import { BaseComponent } from '../../../../shared/components/base/base-component';
import { MultiPackService } from '../../../../services/multi-pack.service';
import { MultiCheckService } from '../../../../services/multi-check.service';
import { DesktopService } from '../../../../services/desktop.service';
import { IMultiPackProgress, IMultiPackRequest } from '../../../../models/multi-pack.models';
import { ICollectUnsignedItem } from '../../../../models/multi-check.models';

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
  private multiCheckService = inject(MultiCheckService);
  protected desktop = inject(DesktopService);

  // Thư mục cha + danh sách hồ sơ đã đạt (allowlist) + trạng thái hiển thị (two-way với cha)
  parentFolderPath = input<string>('');
  passedHoSoNames = input<string[]>([]);
  // File chưa ký số theo từng hồ sơ đạt (để cảnh báo + gom khi đóng gói)
  unsignedItems = input<ICollectUnsignedItem[]>([]);
  visible = model<boolean>(false);

  collecting = signal<boolean>(false);
  unsignedCount = computed(() => this.unsignedItems().reduce((sum, i) => sum + i.fileNames.length, 0));

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

  /** Gom file chưa ký số của các hồ sơ đạt vào thư mục "chưa ký số_{date}" rồi mở cho người dùng xem. */
  onViewUnsigned(): void {
    const items = this.unsignedItems();
    if (!items.length || this.collecting()) return;
    this.collecting.set(true);
    this.multiCheckService.collectUnsignedMulti(this.parentFolderPath(), items).subscribe({
      next: (res) => {
        this.collecting.set(false);
        if (this.isResponseSucceed(res)) {
          this.openFolder(res.data.parentPath);
        }
      },
      error: () => {
        this.collecting.set(false);
        this.messageError('Không gom được file chưa ký số. Vui lòng thử lại.');
      }
    });
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
