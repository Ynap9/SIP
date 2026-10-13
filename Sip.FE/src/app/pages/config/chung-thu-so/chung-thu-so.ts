import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { TagModule } from 'primeng/tag';

import { SharedImports } from '../../../shared/import.shared';
import { BaseComponent } from '../../../shared/components/base/base-component';
import { DataTable } from '../../../shared/components/data-table/data-table';
import { IColumn } from '../../../shared/models/data-table.models';
import { CellViewTypes } from '../../../shared/constants/data-table.constants';
import { SignService } from '../../../services/sign.service';
import { SignCertStateService } from '../../../services/sign-cert-state.service';
import { ISignCert } from '../../../models/sign.models';
import {
  CERT_NOT_SIGNABLE_LABEL,
  CERT_SIGNABLE_LABEL,
  CERT_SOURCE_LABELS,
  CERT_SOURCE_SEVERITIES,
  ECertSource
} from '../../../shared/constants/sign.constants';

interface ICertRow extends ISignCert {
  sourceLabel: string;
  statusLabel: string;
}

@Component({
  selector: 'app-chung-thu-so',
  imports: [...SharedImports, TagModule, DataTable],
  templateUrl: './chung-thu-so.html',
  styleUrl: './chung-thu-so.scss'
})
export class ChungThuSo extends BaseComponent implements OnInit {
  private signService = inject(SignService);
  private certState = inject(SignCertStateService);

  rows = signal<ICertRow[]>([]);
  tableLoading = signal<boolean>(false);
  verifying = signal<boolean>(false);
  onlySignable = signal<boolean>(false);
  selectedThumbprint = signal<string | null>(null);

  visibleRows = computed<ICertRow[]>(() =>
    this.onlySignable() ? this.rows().filter((row) => row.canSign) : this.rows()
  );

  selectedCert = computed<ICertRow | null>(
    () => this.rows().find((row) => row.thumbprint === this.selectedThumbprint()) ?? null
  );

  hasInvalidSelection = computed<boolean>(() => {
    const cert = this.selectedCert();
    return !!cert && !cert.canSign;
  });

  isVerified = computed<boolean>(
    () => !!this.selectedThumbprint() && this.certState.verifiedCert()?.thumbprint === this.selectedThumbprint()
  );

  signableCount = computed<number>(() => this.rows().filter((row) => row.canSign).length);
  tokenCount = computed<number>(() => this.rows().filter((row) => row.source === ECertSource.UsbToken).length);

  columns: IColumn[] = [
    { header: 'STT', cellViewType: CellViewTypes.INDEX, headerContainerStyle: 'width: 70px' },
    { header: 'Chủ sở hữu', field: 'commonName' },
    { header: 'Tổ chức cấp', field: 'issuerCommonName' },
    {
      header: 'Nguồn',
      field: 'sourceLabel',
      cellViewType: CellViewTypes.STATUS,
      statusSeverityFunction: (row: ICertRow) => CERT_SOURCE_SEVERITIES[row.source] ?? 'secondary'
    },
    { header: 'Số serial', field: 'serialNumber' },
    { header: 'Hiệu lực đến', field: 'validTo' },
    {
      header: 'Trạng thái',
      field: 'statusLabel',
      cellViewType: CellViewTypes.STATUS,
      statusSeverityFunction: (row: ICertRow) => (row.canSign ? 'success' : 'danger')
    },
    { header: 'Ghi chú', field: 'reason', cellClass: 'cell-pre-line' }
  ];

  override ngOnInit(): void {
    this.loadCertificates();
  }

  toggleOnlySignable(): void {
    this.onlySignable.update((value) => !value);
  }

  loadCertificates(): void {
    this.tableLoading.set(true);
    this.signService.getCertificates().subscribe({
      next: (res) => {
        if (this.isResponseSucceed(res)) {
          const data: ICertRow[] = (res.data ?? []).map((cert) => ({
            ...cert,
            sourceLabel: CERT_SOURCE_LABELS[cert.source] ?? 'Không xác định',
            statusLabel: cert.canSign ? CERT_SIGNABLE_LABEL : CERT_NOT_SIGNABLE_LABEL
          }));
          this.rows.set(data);

          const verified = this.certState.verifiedCert();
          const signable = data.filter((cert) => cert.canSign);
          if (verified && data.some((cert) => cert.thumbprint === verified.thumbprint)) {
            this.selectedThumbprint.set(verified.thumbprint);
          } else {
            this.selectedThumbprint.set(signable.length === 1 ? signable[0].thumbprint : null);
          }
        } else {
          this.rows.set([]);
          this.selectedThumbprint.set(null);
        }
        this.tableLoading.set(false);
      },
      error: () => {
        this.messageError('Không thể đọc chứng thư số trên máy. Vui lòng thử lại sau.');
        this.rows.set([]);
        this.selectedThumbprint.set(null);
        this.tableLoading.set(false);
      }
    });
  }

  onSelectCert(thumbprint: string): void {
    this.selectedThumbprint.set(thumbprint);
    const cert = this.selectedCert();
    if (!cert) return;
    if (!cert.canSign) {
      this.messageWarning(cert.reason || 'Chứng thư số này không dùng để ký được.');
      return;
    }
    this.verify(thumbprint);
  }

  verify(thumbprint: string): void {
    if (this.verifying()) return;
    this.verifying.set(true);
    this.signService.verifyToken(thumbprint).subscribe({
      next: (res) => {
        this.verifying.set(false);
        if (!this.isResponseSucceed(res)) return;
        const data = res.data;
        if (data?.valid) {
          this.certState.verifiedCert.set(data);
          this.messageSuccess('Chứng thư số đã sẵn sàng để ký.');
        } else {
          this.certState.verifiedCert.set(null);
          this.messageError(
            data?.reason || 'Chứng thư số chưa sẵn sàng để ký. Vui lòng kiểm tra lại token/PIN.'
          );
        }
      },
      error: () => {
        this.verifying.set(false);
        this.messageError('Không kiểm tra được chứng thư số. Vui lòng thử lại sau.');
      }
    });
  }
}
