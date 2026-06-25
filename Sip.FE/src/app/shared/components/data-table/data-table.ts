
import { CommonModule, DatePipe, NgClass, NgComponentOutlet } from '@angular/common';
import { AfterViewInit, Component, ElementRef, EventEmitter, inject, InjectionToken, Injector, input, OnInit, Output, ViewChild } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { Paginator, PaginatorModule } from 'primeng/paginator';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { IColumn } from '../../models/data-table.models';
import { CellViewTypes } from '../../constants/data-table.constants';

export const TBL_CUSTOM_COMP_EMIT = new InjectionToken<EventEmitter<any>>('TBL_CUSTOM_COMP_EMIT');

@Component({
    selector: 'app-data-table',
    imports: [TableModule, PaginatorModule, DatePipe, IconFieldModule, InputIconModule, NgClass, NgComponentOutlet, CommonModule, TagModule],
    templateUrl: './data-table.html',
    styleUrl: './data-table.scss'
})
export class DataTable implements OnInit, AfterViewInit {
    injector = inject(Injector);
    private el = inject(ElementRef);

    columns  = input.required<IColumn[]>();
    data     = input.required<any[]>();
    pageSize   = input<number>(10);
    pageNumber = input<number>(1);
    total      = input<number>(100);
    loading    = input<boolean>(false);
    tableKey   = input<string>('');
    hidePaginator = input<boolean>(false);

    @Output() onPageChanged = new EventEmitter<any>();
    @Output() customEmit    = new EventEmitter<any>();
    @Output() onCustomComp  = new EventEmitter<any>();
    @Output() onError       = new EventEmitter<any>();

    cellViewTypes   = CellViewTypes;
    sanitizer       = inject(DomSanitizer);
    customInjector! : Injector;
    revealedSecrets = new Set<string>();
    @ViewChild(Paginator) paginator!: Paginator;

    private readonly STORAGE_PREFIX = 'tbl_col_widths_';
    private colWidths: Record<number, number> = {};

    ngOnInit(): void {
        this.customInjector = Injector.create({
            providers: [{ provide: TBL_CUSTOM_COMP_EMIT, useValue: this.customEmit }],
            parent: this.injector
        });
        this.customEmit.subscribe((data) => this.onTblCustomCompEmit(data));
        this.loadColWidths();
    }

    ngAfterViewInit(): void {
        // Áp dụng cached width trực tiếp lên DOM — KHÔNG dùng [style] binding
        // để PrimeNG resize handler vẫn hoạt động bình thường sau khi load cache
        this.applyCachedWidthsToDom();
    }

    // ─── Column resize cache ───────────────────────────────────────────────

    private loadColWidths(): void {
        const key = this.tableKey();
        if (!key) return;
        try {
            const saved = localStorage.getItem(this.STORAGE_PREFIX + key);
            this.colWidths = saved ? JSON.parse(saved) : {};
        } catch {
            this.colWidths = {};
        }
    }

    private saveColWidths(): void {
        const key = this.tableKey();
        if (!key) return;
        localStorage.setItem(this.STORAGE_PREFIX + key, JSON.stringify(this.colWidths));
    }

    private applyCachedWidthsToDom(): void {
        if (Object.keys(this.colWidths).length === 0) return;
        // setTimeout để chờ PrimeNG render xong table
        setTimeout(() => {
            const ths = this.el.nativeElement.querySelectorAll('table thead tr th') as NodeListOf<HTMLElement>;
            ths.forEach((th: HTMLElement, index: number) => {
                const cached = this.colWidths[index];
                if (cached) {
                    // Chỉ set width, KHÔNG set min-width — để PrimeNG resize tự do
                    th.style.width = `${cached}px`;
                }
            });
        });
    }

    onColResize(event: any): void {
        const th = event.element as HTMLElement;
        const index = Array.from(th.parentElement?.children ?? []).indexOf(th);
        if (index >= 0) {
            this.colWidths[index] = th.offsetWidth;
            this.saveColWidths();
        }
    }

    // ─── Existing logic (không thay đổi) ──────────────────────────────────

    onTblCustomCompEmit(data: any) {
        this.onCustomComp.emit(data);
    }

    get<T>(obj: any, path: string): T | undefined {
        return path.split('.').reduce((acc, key) => acc && acc[key], obj);
    }

    onPage($event: any) {
        this.onPageChanged.emit($event);
    }

    onCellClick(col: IColumn, row: any) {
        if (col.clickable || col.cellViewType === CellViewTypes.CHECKBOX) {
            this.onCustomComp.emit({ type: 'cellClick', field: col.field, data: row });
        }
    }

    isCellClickable(col: IColumn): boolean {
        return col.clickable === true;
    }

    isAllChecked(field: string): boolean {
        const dataArr = this.data();
        return dataArr.length > 0 && dataArr.every(row => this.get<boolean>(row, field));
    }

    onHeaderCheckboxClick(col: IColumn): void {
        this.onCustomComp.emit({ type: 'headerCheckbox', field: col.field });
    }

    formatVND(value: number | string | null | undefined): string {
        if (value == null || value === '' || value === undefined) return '';
        return value.toString().replace(/\B(?=(\d{3})+(?!\d))/g, '.');
    }

    getIndexValue(rowIndex: number): number {
        const currentPage = this.pageNumber();
        const pageSize    = this.pageSize();
        return (currentPage * pageSize) - (pageSize - rowIndex) + 1;
    }

    goToPageNumber(pageNumber: number): void {
        const totalPages = Math.ceil(this.total() / this.pageSize());

        if (pageNumber < 1 || pageNumber > totalPages) {
            this.onError.emit(`Số trang không hợp lệ! Vui lòng nhập từ 1 đến ${totalPages}`);
            return;
        }

        const first = (pageNumber - 1) * this.pageSize();

        if (this.paginator) {
            this.paginator.changePage(pageNumber - 1);
        }

        this.onPageChanged.emit({ first, rows: this.pageSize(), page: pageNumber - 1, pageCount: totalPages });
    }

    isSecretVisible(rowIndex: number, field?: string): boolean {
        return this.revealedSecrets.has(`${rowIndex}-${field}`);
    }

    toggleSecret(rowIndex: number, field?: string): void {
        const key = `${rowIndex}-${field}`;
        if (this.revealedSecrets.has(key)) {
            this.revealedSecrets.delete(key);
        } else {
            this.revealedSecrets.add(key);
        }
    }
}