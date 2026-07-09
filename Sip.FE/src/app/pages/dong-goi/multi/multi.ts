import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { ProgressBarModule } from 'primeng/progressbar';

import { SharedImports } from '../../../shared/import.shared';
import { BaseComponent } from '../../../shared/components/base/base-component';
import { Utils } from '../../../shared/utils';
import { MultiCheckService } from '../../../services/multi-check.service';
import { DesktopService } from '../../../services/desktop.service';
import { IMultiCheckProgress, IHoSoRow, IFileRow, ICollectUnsignedItem } from '../../../models/multi-check.models';
import { PackageMultiDialog } from './package-multi-dialog/package-multi-dialog';

@Component({
  selector: 'app-multi',
  imports: [...SharedImports, ProgressBarModule, PackageMultiDialog],
  templateUrl: './multi.html',
  styleUrl: './multi.scss'
})
export class Multi extends BaseComponent implements OnDestroy {
  private multiCheckService = inject(MultiCheckService);
  protected desktop = inject(DesktopService);

  folderPath = signal<string>('');
  isDragOver = signal<boolean>(false);

  showPackageDialog = signal<boolean>(false);

  running = signal<boolean>(false);
  percent = signal<number>(0);
  completed = signal<boolean>(false);
  hoSoRows = signal<IHoSoRow[]>([]);
  expandedRows = signal<Set<string>>(new Set());

  // Hồ sơ đã đạt -> allowlist gửi khi đóng gói (chỉ gói hồ sơ pass).
  passedHoSoNames = computed(() => this.hoSoRows().filter((r) => r.status === 'ok').map((r) => r.name));

  // File chưa ký số theo từng hồ sơ ĐẠT (để cảnh báo + gom khi đóng gói).
  unsignedItems = computed<ICollectUnsignedItem[]>(() =>
    this.hoSoRows()
      .filter((r) => r.status === 'ok')
      .map((r) => ({
        hoSoName: r.name,
        fileNames: r.files.filter((f) => f.signature && !f.signature.isSigned).map((f) => f.name)
      }))
      .filter((i) => i.fileNames.length > 0)
  );

  private sub?: Subscription;

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  // ===== Drag & drop thư mục cha (desktop expose path tuyệt đối) =====
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
    const path = file?.path;
    if (path) {
      this.folderPath.set(Utils.normalizeFolderPath(path));
    } else {
      this.messageWarning('Không lấy được đường dẫn thư mục. Vui lòng dán đường dẫn vào ô bên dưới.');
    }
  }

  canCheck(): boolean {
    return !!this.folderPath().trim() && !this.running();
  }

  async chooseFolder(): Promise<void> {
    const path = await this.desktop.pickFolder();
    if (path) {
      this.folderPath.set(Utils.normalizeFolderPath(path));
    }
  }

  // ===== Xổ/thu bảng con file của 1 hồ sơ (tự quản, không dùng row-expansion của p-table) =====
  isExpanded(name: string): boolean {
    return this.expandedRows().has(name);
  }

  toggleRow(name: string): void {
    this.expandedRows.update((set) => {
      const next = new Set(set);
      if (next.has(name)) next.delete(name);
      else next.add(name);
      return next;
    });
  }

  // ===== Chạy kiểm tra nhiều hồ sơ (SSE realtime) =====
  startCheck(): void {
    const path = Utils.normalizeFolderPath(this.folderPath());
    if (!path) return;

    this.sub?.unsubscribe();
    this.running.set(true);
    this.completed.set(false);
    this.percent.set(0);
    this.hoSoRows.set([]);
    this.expandedRows.set(new Set());

    this.sub = this.multiCheckService.checkMulti(path).subscribe({
      next: (ev) => this.handleEvent(ev),
      error: () => {
        this.running.set(false);
        this.messageError('Không kết nối được tới dịch vụ kiểm tra. Vui lòng thử lại sau.');
      },
      complete: () => this.running.set(false)
    });
  }

  private handleEvent(ev: IMultiCheckProgress): void {
    if (typeof ev.percent === 'number') {
      this.percent.set(ev.percent);
    }

    if (ev.targetType === 'manifest') {
      // Seed sẵn mọi dòng hồ sơ (spinner) trước khi có kết quả.
      this.hoSoRows.set((ev.hoSoNames ?? []).map((name) => ({ name, status: 'processing' as const, files: [] })));
    } else if (ev.targetType === 'hoso') {
      this.applyHoSo(ev);
    } else if (ev.targetType === 'parent') {
      // Lỗi nghiêm trọng cấp thư mục cha (dừng cả batch)
      this.messageError(ev.message);
    }

    if (ev.completed) {
      this.percent.set(100);
      this.completed.set(true);
      // Hồ sơ còn processing (nếu có) -> coi như đạt
      this.hoSoRows.update((rows) => rows.map((r) => (r.status === 'processing' ? { ...r, status: 'ok' } : r)));
    }
  }

  private applyHoSo(ev: IMultiCheckProgress): void {
    const files: IFileRow[] = (ev.files ?? []).map((f) => ({
      name: f.name,
      status: f.status === 0 ? 'error' : 'ok',
      reason: f.status === 0 ? f.reason : undefined,
      signature: f.signature
    }));
    const status: IHoSoRow['status'] = ev.status === 0 ? 'error' : 'ok';
    const reason = ev.status === 0 ? (ev.errors?.join(' | ') || ev.message) : undefined;

    this.hoSoRows.update((rows) => {
      const idx = rows.findIndex((r) => r.name === ev.hoSoName);
      const row: IHoSoRow = { name: ev.hoSoName, status, reason, files };
      if (idx === -1) return [...rows, row];
      return rows.map((r, i) => (i === idx ? row : r));
    });
  }

  // ===== Đóng gói: đủ điều kiện (đã kiểm tra, có hồ sơ đạt) -> mở popup =====
  onPackage(): void {
    if (!this.completed()) {
      this.messageWarning('Vui lòng kiểm tra trước khi đóng gói.');
      return;
    }
    if (this.passedHoSoNames().length === 0) {
      this.messageWarning('Không có hồ sơ nào đạt để đóng gói.');
      return;
    }
    this.showPackageDialog.set(true);
  }
}
