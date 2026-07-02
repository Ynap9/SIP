import { Component, inject, signal } from '@angular/core';
import { MenuItem, TreeNode } from 'primeng/api';
import { ProgressBarModule } from 'primeng/progressbar';
import { TreeModule } from 'primeng/tree';

import { SharedImports } from '../../shared/import.shared';
import { BaseComponent } from '../../shared/components/base/base-component';
import { Breadcrumb } from '../../shared/components/breadcrumb/breadcrumb';
import { Utils } from '../../shared/utils';
import { UnpackService } from '../../services/unpack.service';
import { DesktopService } from '../../services/desktop.service';
import { IUnpackResult, IFileNode } from '../../models/unpack.models';

@Component({
  selector: 'app-boc-tach',
  imports: [...SharedImports, ProgressBarModule, TreeModule, Breadcrumb],
  templateUrl: './boc-tach.html',
  styleUrl: './boc-tach.scss'
})
export class BocTach extends BaseComponent {
  private unpackService = inject(UnpackService);
  protected desktop = inject(DesktopService);

  home: MenuItem = { icon: 'pi pi-home', routerLink: '/' };
  breadcrumbItems: MenuItem[] = [{ label: 'Bóc tách dữ liệu' }];

  sourcePath = signal<string>('');
  outputPath = signal<string>('');
  isDragOver = signal<boolean>(false);
  isLoading = signal<boolean>(false);
  result = signal<IUnpackResult | null>(null);
  treeNodes = signal<TreeNode[]>([]);

  // ===== Kéo-thả gói zip (desktop expose path tuyệt đối) =====
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
      this.sourcePath.set(Utils.normalizeFolderPath(path));
    } else {
      this.messageWarning('Không lấy được đường dẫn. Vui lòng dán vào ô bên dưới.');
    }
  }

  async chooseOutput(): Promise<void> {
    const path = await this.desktop.pickFolder();
    if (path) {
      this.outputPath.set(Utils.normalizeFolderPath(path));
    }
  }

  canUnpack(): boolean {
    return !!this.sourcePath().trim() && !!this.outputPath().trim() && !this.isLoading();
  }

  // ===== Bóc tách (= lưu luôn vào thư mục xuất) =====
  onUnpack(): void {
    const sourcePath = Utils.normalizeFolderPath(this.sourcePath());
    const outputPath = Utils.normalizeFolderPath(this.outputPath());
    if (!sourcePath) {
      this.messageWarning('Vui lòng chọn/dán gói nguồn (.zip hoặc thư mục gói).');
      return;
    }
    if (!outputPath) {
      this.messageWarning('Vui lòng nhập thư mục xuất ra.');
      return;
    }

    this.isLoading.set(true);
    this.result.set(null);
    this.treeNodes.set([]);

    this.unpackService.unpack({ sourcePath, outputPath }).subscribe({
      next: (res) => {
        this.isLoading.set(false);
        if (this.isResponseSucceed(res)) {
          this.result.set(res.data);
          this.treeNodes.set(res.data.tree ? [this.toTreeNode(res.data.tree)] : []);
        }
      },
      error: () => {
        this.isLoading.set(false);
        this.messageError('Bóc tách thất bại. Vui lòng thử lại.');
      }
    });
  }

  private toTreeNode(node: IFileNode): TreeNode {
    const isFolder = node.type === 'folder';
    return {
      label: node.name,
      data: node.path,
      icon: isFolder ? 'pi pi-folder' : 'pi pi-file',
      expanded: isFolder,
      leaf: !isFolder,
      children: node.children?.map((c) => this.toTreeNode(c))
    };
  }

  // Click tên folder -> xổ/thu cây; click tên file -> mở file bằng trình xem mặc định.
  onNodeSelect(node: TreeNode): void {
    if (node.leaf) {
      this.openNode(node.data as string);
    } else {
      node.expanded = !node.expanded;
      this.treeNodes.set([...this.treeNodes()]); // đổi tham chiếu để p-tree render lại theo expanded mới
    }
  }

  // Mở thư mục/file: desktop -> Explorer / app mặc định; web-dev -> copy clipboard.
  openNode(path: string): void {
    if (!path) return;
    if (this.desktop.openPath(path)) return;
    navigator.clipboard?.writeText(path).then(
      () => this.messageSuccess('Đã sao chép đường dẫn vào clipboard.'),
      () => this.messageWarning('Không mở được. Đường dẫn: ' + path)
    );
  }
}
