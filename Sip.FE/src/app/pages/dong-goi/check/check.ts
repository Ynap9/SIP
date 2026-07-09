import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { ProgressBarModule } from 'primeng/progressbar';
import { TableModule } from 'primeng/table';

import { SharedImports } from '../../../shared/import.shared';
import { BaseComponent } from '../../../shared/components/base/base-component';
import { Utils } from '../../../shared/utils';
import { CheckService } from '../../../services/check.service';
import { DesktopService } from '../../../services/desktop.service';
import { ETimestampVerdict, ICheckProgress, IFileRow } from '../../../models/check.models';
import { PackageDialog } from './package-dialog/package-dialog';

@Component({
  selector: 'app-check',
  imports: [...SharedImports, ProgressBarModule, TableModule, PackageDialog],
  templateUrl: './check.html',
  styleUrl: './check.scss'
})
export class Check extends BaseComponent implements OnDestroy {
  private checkService = inject(CheckService);
  protected desktop = inject(DesktopService);

  folderPath = signal<string>('');
  isDragOver = signal<boolean>(false);

  // Hiển thị popup đóng gói
  showPackageDialog = signal<boolean>(false);

  running = signal<boolean>(false);
  percent = signal<number>(0);
  completed = signal<boolean>(false);
  fileRows = signal<IFileRow[]>([]);

  // Bảng hiển thị sắp theo SỐ THỨ TỰ sau mã hồ sơ (vd .1 < .2 < .10); nguồn cập nhật vẫn là fileRows.
  sortedFileRows = computed(() =>
    [...this.fileRows()].sort((a, b) => Utils.compareDocSeq(a.name, b.name))
  );

  // File chưa ký số (đã có thông tin ký số và isSigned=false) -> cảnh báo khi đóng gói.
  unsignedFileNames = computed(() =>
    this.fileRows().filter((r) => r.signature && !r.signature.isSigned).map((r) => r.name)
  );

  // Enum verdict dấu thời gian để dùng trong template.
  protected readonly EVerdict = ETimestampVerdict;

  // Có file không phải PDF (verdict NotApplicable) -> trình kiểm tra ký số đang phát triển -> cảnh báo khi đóng gói.
  hasNonPdf = computed(() =>
    this.fileRows().some((r) => r.signature?.timestamp?.verdict === ETimestampVerdict.NotApplicable)
  );

  // Dòng cuối: thông tin tổng kết hồ sơ
  hoSo = signal<{ status: 'processing' | 'ok' | 'error'; reason: string }>({
    status: 'processing',
    reason: ''
  });
  private hoSoErrors: string[] = [];

  private sub?: Subscription;

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  // ===== Drag & drop thư mục (desktop expose path tuyệt đối) =====
  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(false);
    const file = event.dataTransfer?.files?.[0] as (File & { path?: string }) | undefined;
    const path = file?.path; // Electron/Tauri: File có path = đường dẫn tuyệt đối
    if (path) {
      this.folderPath.set(Utils.normalizeFolderPath(path));
    } else {
      this.messageWarning('Không lấy được đường dẫn thư mục. Vui lòng dán đường dẫn vào ô bên dưới.');
    }
  }

  canCheck(): boolean {
    return !!this.folderPath().trim() && !this.running();
  }

  // ===== Chọn thư mục bằng dialog native (desktop) =====
  async chooseFolder(): Promise<void> {
    const path = await this.desktop.pickFolder();
    if (path) {
      this.folderPath.set(Utils.normalizeFolderPath(path));
    }
  }

  // ===== Chạy kiểm tra (SSE realtime) =====
  startCheck(): void {
    const path = Utils.normalizeFolderPath(this.folderPath());
    if (!path) return;

    this.sub?.unsubscribe();
    this.running.set(true);
    this.completed.set(false);
    this.percent.set(0);
    this.fileRows.set([]);
    this.hoSoErrors = [];
    this.hoSo.set({ status: 'processing', reason: '' });

    this.sub = this.checkService.checkFolder(path).subscribe({
      next: (ev) => this.handleEvent(ev),
      error: () => {
        this.running.set(false);
        this.messageError('Không kết nối được tới dịch vụ kiểm tra. Vui lòng thử lại sau.');
      },
      complete: () => this.running.set(false)
    });
  }

  private handleEvent(ev: ICheckProgress): void {
    if (typeof ev.percent === 'number') {
      this.percent.set(ev.percent);
    }

    if (ev.targetType === 'manifest') {
      this.fileRows.set((ev.files ?? []).map((name) => ({ name, status: 'processing' as const })));
    } else {
      const fileName = this.resolveFileName(ev);
      if (fileName) {
        this.applyFileStatus(fileName, ev);
      } else if (ev.status === 0 && ev.targetType !== 'summary' && !ev.completed) {
        // Lỗi cấp hồ sơ (không gắn file) -> gom vào dòng tổng kết hồ sơ
        this.hoSoErrors.push(ev.message);
      }
    }

    if (ev.completed) {
      this.percent.set(100);
      this.completed.set(true);
      // Đã check xong -> mọi dòng file còn processing chuyển sang "đã xử lý"
      this.fileRows.update((rows) =>
        rows.map((r) => (r.status === 'processing' ? { ...r, status: 'ok' } : r))
      );
      this.hoSo.set({
        status: ev.status === 0 ? 'error' : 'ok',
        reason: this.hoSoErrors.length ? this.hoSoErrors.join(' | ') : ev.message
      });
    }
  }

  /**
   * Tên file mà event liên quan:
   * - targetType 'file': target = tên file (giai đoạn 1/2)
   * - targetType 'field' (Van_ban): nhãn dòng (trước ' / ') = mã lưu trữ = tên file
   */
  private resolveFileName(ev: ICheckProgress): string | null {
    if (ev.targetType === 'file') {
      return ev.target;
    }
    if (ev.targetType === 'field' && ev.target.includes(' / ')) {
      const label = ev.target.split(' / ')[0].trim();
      if (this.fileRows().some((r) => this.matchFile(r.name, label))) {
        return label;
      }
    }
    return null;
  }

  private matchFile(rowName: string, key: string): boolean {
    if (rowName === key) return true;
    const dot = rowName.lastIndexOf('.');
    const noExt = dot > 0 ? rowName.slice(0, dot) : rowName;
    return noExt === key;
  }

  private applyFileStatus(key: string, ev: ICheckProgress): void {
    this.fileRows.update((rows) => {
      const idx = rows.findIndex((r) => this.matchFile(r.name, key));
      if (idx === -1) {
        if (ev.targetType === 'file') {
          return [
            ...rows,
            {
              name: key,
              status: ev.status === 0 ? 'error' : 'ok',
              reason: ev.status === 0 ? ev.message : undefined,
              signature: ev.signature
            }
          ];
        }
        return rows;
      }
      const r = rows[idx];
      // Gộp thông tin ký số nếu event mang theo (độc lập với trạng thái đúng/lỗi).
      const base: IFileRow = ev.signature ? { ...r, signature: ev.signature } : r;
      let updated: IFileRow;
      if (base.status === 'error') {
        updated = base; // lỗi đầu tiên thắng (chỉ nhận thêm signature)
      } else if (ev.status === 0) {
        updated = { ...base, status: 'error', reason: ev.message };
      } else {
        updated = { ...base, status: 'ok' };
      }
      return rows.map((x, i) => (i === idx ? updated : x));
    });
  }

  // ===== Đóng gói: đủ điều kiện -> mở popup; không đủ -> toast =====
  onPackage(): void {
    if (!this.completed() || this.hoSo().status !== 'ok') {
      this.messageWarning('Hồ sơ không đủ điều kiện! Vui lòng kiểm tra lại hoặc liên hệ admin!');
      return;
    }
    // Có file không phải PDF: cảnh báo trình kiểm tra ký số đang phát triển, vẫn cho đóng gói nếu người dùng đồng ý.
    if (this.hasNonPdf()) {
      this.confirmAction(
        {
          header: 'Có file không phải PDF',
          message:
            'Đang phát triển trình kiểm tra ký số cho file không phải PDF, vui lòng liên hệ admin. Bạn vẫn muốn đóng gói?'
        },
        () => this.showPackageDialog.set(true)
      );
      return;
    }
    this.showPackageDialog.set(true);
  }
}
