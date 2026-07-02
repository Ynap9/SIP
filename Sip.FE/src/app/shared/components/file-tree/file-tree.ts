import { Component, input, output, signal } from '@angular/core';
import { NgClass, NgTemplateOutlet } from '@angular/common';
import { IFileNode } from '../../../models/unpack.models';

/**
 * Cây thư mục kiểu library, đệ quy bằng ng-template (không dùng PrimeNG p-tree để tránh lỗi tràn ngăn xếp).
 * - Click tên FOLDER: xổ/thu nhánh con.
 * - Click tên FILE: emit `open` (đường dẫn) để component cha mở file.
 */
@Component({
  selector: 'app-file-tree',
  imports: [NgClass, NgTemplateOutlet],
  templateUrl: './file-tree.html',
  styleUrl: './file-tree.scss'
})
export class FileTree {
  nodes = input<IFileNode[]>([]);
  open = output<string>();

  // Tập folder ĐANG THU (mặc định mở hết); toggle theo node.path.
  private collapsed = signal<Set<string>>(new Set());

  isFolder(node: IFileNode): boolean {
    return node.type === 'folder';
  }

  isExpanded(node: IFileNode): boolean {
    return !this.collapsed().has(node.path);
  }

  onClickNode(node: IFileNode): void {
    if (this.isFolder(node)) {
      this.collapsed.update((set) => {
        const next = new Set(set);
        if (next.has(node.path)) next.delete(node.path);
        else next.add(node.path);
        return next;
      });
    } else {
      this.open.emit(node.path);
    }
  }
}
