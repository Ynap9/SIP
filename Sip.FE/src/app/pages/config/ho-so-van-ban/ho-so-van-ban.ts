import { Component, inject, OnInit, signal } from '@angular/core';
import { SharedImports } from '../../../shared/import.shared';
import { BaseComponent } from '../../../shared/components/base/base-component';
import { DataTable } from '../../../shared/components/data-table/data-table';
import { IColumn } from '../../../shared/models/data-table.models';
import { CellViewTypes } from '../../../shared/constants/data-table.constants';
import { ConfigService } from '../../../services/config.service';
import { IConfigItem } from '../../../models/config.models';

@Component({
  selector: 'app-ho-so-van-ban',
  imports: [...SharedImports, DataTable],
  templateUrl: './ho-so-van-ban.html',
  styleUrl: './ho-so-van-ban.scss'
})
export class HoSoVanBan extends BaseComponent implements OnInit {
  private configService = inject(ConfigService);

  options = [
    { label: 'Hồ sơ', value: 'ho-so' },
    { label: 'Văn bản', value: 'van-ban' }
  ];

  selected = signal<string | null>(null);
  rows = signal<IConfigItem[]>([]);
  tableLoading = signal<boolean>(false);

  columns: IColumn[] = [
    { header: 'STT', cellViewType: CellViewTypes.INDEX },
    { header: 'Trường thông tin', field: 'truongThongTin' },
    { header: 'Mô tả', field: 'moTa', cellClass: 'cell-pre-line' },
    { header: 'Bắt buộc', field: 'batBuoc', cellViewType: CellViewTypes.BOOL_CHECK }
  ];

  override ngOnInit(): void {
    this.selected.set('ho-so');
    this.onSelect();
  }

  onSelect(): void {
    const value = this.selected();
    if (!value) {
      this.rows.set([]);
      return;
    }

    this.tableLoading.set(true);
    const request$ =
      value === 'ho-so'
        ? this.configService.getConfigHoSo()
        : this.configService.getConfigVanBan();

    request$.subscribe({
      next: (res) => {
        if (this.isResponseSucceed(res)) {
          // BE trả "\n" dạng ký tự literal -> đổi sang xuống dòng thật để hiển thị đúng
          const data = (res.data ?? []).map((item) => ({
            ...item,
            moTa: (item.moTa ?? '').replace(/\\r\\n|\\n|\\r/g, '\n')
          }));
          this.rows.set(data);
        } else {
          this.rows.set([]);
        }
        this.tableLoading.set(false);
      },
      error: () => {
        this.messageError('Không thể tải dữ liệu cấu hình. Vui lòng thử lại sau.');
        this.rows.set([]);
        this.tableLoading.set(false);
      }
    });
  }
}
