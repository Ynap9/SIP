import { Component, computed, inject, OnInit, output, signal } from '@angular/core';
import { TagModule } from 'primeng/tag';

import { SharedImports } from '../../../shared/import.shared';
import { BaseComponent } from '../../../shared/components/base/base-component';
import { DataTable } from '../../../shared/components/data-table/data-table';
import { IColumn } from '../../../shared/models/data-table.models';
import { CellViewTypes } from '../../../shared/constants/data-table.constants';
import { SignService } from '../../../services/sign.service';
import { ISignCert, ITokenVerify } from '../../../models/sign.models';
import {
  CERT_NOT_SIGNABLE_LABEL,
  CERT_SIGNABLE_LABEL,
  CERT_SOURCE_LABELS,
  CERT_SOURCE_SEVERITIES,
  ECertSource
} from '../../../shared/constants/sign.constants';

// 1 dòng bảng = 1 cert + các trường dựng sẵn để hiển thị (data-table đọc thẳng field, không gọi hàm).
interface ICertRow extends ISignCert {
  sourceLabel: string;
  statusLabel: string;
}

@Component({
  selector: 'app-cert-step',
  // TagModule không nằm trong SharedImports -> import trực tiếp tại trang.
  imports: [...SharedImports, TagModule, DataTable],
  templateUrl: './cert-step.html',
  styleUrl: './cert-step.scss'
})
export class CertStep extends BaseComponent implements OnInit {
  private signService = inject(SignService);

  // Kiểm token OK -> phát chứng thư đã xác thực để shell sang bước ký.
  verified = output<ITokenVerify>();

  rows = signal<ICertRow[]>([]);
  tableLoading = signal<boolean>(false);
  verifying = signal<boolean>(false);
  /** Thumbprint của cert đang chọn (thumbprint duy nhất toàn cục, serial thì không). */
  selectedThumbprint = signal<string | null>(null);

  /** Cert đang chọn, null nếu chưa chọn. */
  selectedCert = computed<ICertRow | null>(
    () => this.rows().find((row) => row.thumbprint === this.selectedThumbprint()) ?? null
  );

  /** Đã chọn nhưng cert KHÔNG ký được -> hiện cảnh báo chặn. */
  hasInvalidSelection = computed<boolean>(() => {
    const cert = this.selectedCert();
    return !!cert && !cert.canSign;
  });

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

  /** Nạp danh sách cert. Lấy HẾT (kể cả cert không ký được) để người dùng thấy lý do, không phải list rỗng. */
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

          // Chỉ có đúng 1 cert ký được -> chọn sẵn, đỡ một thao tác cho trường hợp phổ biến nhất (1 token).
          const signable = data.filter((cert) => cert.canSign);
          this.selectedThumbprint.set(signable.length === 1 ? signable[0].thumbprint : null);
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

  /**
   * Chọn cert -> kiểm luôn qua kiem-tra-token (hộp nhập PIN của token bật lên tại đây). valid=true thì
   * TỰ ĐỘNG sang bước ký; cert không ký được thì cảnh báo và không gọi API.
   */
  onSelectCert(thumbprint: string): void {
    this.selectedThumbprint.set(thumbprint);
    const cert = this.selectedCert();
    if (!cert) return;
    if (!cert.canSign) {
      this.messageWarning(cert.reason || 'Chứng thư số này không dùng để ký được.');
      return;
    }
    this.verifyAndAdvance(thumbprint);
  }

  /**
   * Kiểm chứng thư số qua kiem-tra-token rồi tự sang bước ký khi hợp lệ.
   * valid=true mới emit verified; ngược lại giữ nguyên bước 1 và báo bước hỏng.
   */
  private verifyAndAdvance(thumbprint: string): void {
    if (this.verifying()) return;
    this.verifying.set(true);
    this.signService.verifyToken(thumbprint).subscribe({
      next: (res) => {
        this.verifying.set(false);
        if (!this.isResponseSucceed(res)) return;
        const data = res.data;
        if (data?.valid) {
          this.verified.emit(data);
        } else {
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
