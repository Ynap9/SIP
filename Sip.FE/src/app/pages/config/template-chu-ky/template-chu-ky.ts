import { Component, inject, OnInit, signal } from '@angular/core';

import { SharedImports } from '../../../shared/import.shared';
import { BaseComponent } from '../../../shared/components/base/base-component';
import { DataTable } from '../../../shared/components/data-table/data-table';
import { IColumn, ICustomEmit } from '../../../shared/models/data-table.models';
import { CellViewTypes } from '../../../shared/constants/data-table.constants';
import { SIGN_TEMPLATE_NEW_ID } from '../../../shared/constants/sign.constants';
import { SignTemplateService } from '../../../services/sign-template.service';
import { IViewSignTemplate } from '../../../models/sign-template.models';
import { TemplateTblAction, TemplateTblActionTypes } from './tbl-action/tbl-action';

interface ITemplateRow extends IViewSignTemplate {
  positionCount: number;
}

@Component({
  selector: 'app-template-chu-ky',
  imports: [...SharedImports, DataTable],
  templateUrl: './template-chu-ky.html',
  styleUrl: './template-chu-ky.scss'
})
export class TemplateChuKy extends BaseComponent implements OnInit {
  private signTemplateService = inject(SignTemplateService);

  rows = signal<ITemplateRow[]>([]);
  tableLoading = signal<boolean>(false);
  total = signal<number>(0);
  pageNumber = signal<number>(this.START_PAGE_NUMBER);
  keyword = signal<string>('');
  searchedKeyword = signal<string>('');

  columns: IColumn[] = [
    { header: 'STT', cellViewType: CellViewTypes.INDEX, headerContainerStyle: 'width: 70px', cellStyle: 'text-align: center' },
    { header: 'Tên template', field: 'tenTemplate', cellStyle: 'text-align: center' },
    { header: 'Chứng thư số', field: 'tenChungThu', cellStyle: 'text-align: center' },
    { header: 'Lý do ký', field: 'lyDoKy', cellStyle: 'text-align: center' },
    { header: 'Nơi ký', field: 'noiKy', cellStyle: 'text-align: center' },
    { header: 'Số khối', field: 'positionCount', headerContainerStyle: 'width: 100px', cellStyle: 'text-align: center' },
    {
      header: 'Ngày tạo',
      field: 'createdDate',
      cellViewType: CellViewTypes.DATE,
      dateFormat: 'dd/MM/yyyy HH:mm',
      headerContainerStyle: 'width: 160px',
      cellStyle: 'text-align: center'
    },
    {
      header: 'Thao tác',
      cellViewType: CellViewTypes.CUSTOM_COMP,
      customComponent: TemplateTblAction,
      headerContainerStyle: 'width: 120px'
    }
  ];

  override ngOnInit(): void {
    this.loadTemplates();
  }

  loadTemplates(): void {
    this.tableLoading.set(true);
    this.signTemplateService
      .findPaging({
        pageNumber: this.pageNumber(),
        pageSize: this.MAX_PAGE_SIZE,
        keyword: this.searchedKeyword() || undefined
      })
      .subscribe({
        next: (res) => {
          if (this.isResponseSucceed(res)) {
            this.rows.set(
              (res.data.items ?? []).map((item) => ({ ...item, positionCount: item.positions?.length ?? 0 }))
            );
            this.total.set(res.data.totalItems ?? 0);
          } else {
            this.rows.set([]);
            this.total.set(0);
          }
          this.tableLoading.set(false);
        },
        error: () => {
          this.messageError('Không tải được danh sách template chữ ký. Vui lòng thử lại sau.');
          this.rows.set([]);
          this.total.set(0);
          this.tableLoading.set(false);
        }
      });
  }

  onSearch(): void {
    this.searchedKeyword.set(this.keyword().trim());
    this.pageNumber.set(this.START_PAGE_NUMBER);
    this.loadTemplates();
  }

  onClearSearch(): void {
    this.keyword.set('');
    this.onSearch();
  }

  onPageChanged(event: { page: number }): void {
    this.pageNumber.set(event.page + 1);
    this.loadTemplates();
  }

  onCreate(): void {
    this.router.navigate([SIGN_TEMPLATE_NEW_ID], { relativeTo: this._activatedRoute });
  }

  onTableAction(event: ICustomEmit<IViewSignTemplate>): void {
    if (event.type === TemplateTblActionTypes.update) {
      this.router.navigate([event.data.id], { relativeTo: this._activatedRoute });
    } else if (event.type === TemplateTblActionTypes.delete) {
      this.confirmDelete(
        {
          header: 'Xoá template',
          message: `Xoá template "${event.data.tenTemplate}"? Ảnh dấu và chữ ký tươi của template cũng bị xoá.`
        },
        () => this.deleteTemplate(event.data.id)
      );
    }
  }

  private deleteTemplate(id: number): void {
    this.signTemplateService.delete(id).subscribe({
      next: (res) => {
        if (!this.isResponseSucceed(res, true, 'Đã xoá template.')) return;
        if (this.rows().length === 1 && this.pageNumber() > this.START_PAGE_NUMBER) {
          this.pageNumber.update((page) => page - 1);
        }
        this.loadTemplates();
      },
      error: () => this.messageError('Không xoá được template. Vui lòng thử lại sau.')
    });
  }
}
