import { Component, OnDestroy, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Subscription } from 'rxjs';
import { ProgressBarModule } from 'primeng/progressbar';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';

import { SharedImports } from '../../../shared/import.shared';
import { BaseComponent } from '../../../shared/components/base/base-component';
import { Utils } from '../../../shared/utils';
import { DesktopService } from '../../../services/desktop.service';
import { SignService } from '../../../services/sign.service';
import { SignCertStateService } from '../../../services/sign-cert-state.service';
import {
  IPdfFormatProgress,
  IPdfFormatSummary,
  ISignConfig,
  ISignFileRow,
  ISignPositionCheck,
  ISignProgress,
  ISignRequest,
  ISignResult
} from '../../../models/sign.models';
import {
  EPdfFormatVerdict,
  PDF_FORMAT_VERDICT_LABELS,
  PDF_FORMAT_VERDICT_SEVERITIES,
  SIGN_DEFAULT_CONFIG
} from '../../../shared/constants/sign.constants';
import { SignDialog } from './sign-dialog/sign-dialog';
import { SignResultDialog } from './sign-result-dialog/sign-result-dialog';

@Component({
  selector: 'app-sign-step',
  // TableModule/ProgressBarModule/TagModule không nằm trong SharedImports -> import trực tiếp tại trang.
  imports: [...SharedImports, ProgressBarModule, TableModule, TagModule, SignDialog, SignResultDialog],
  templateUrl: './sign-step.html',
  styleUrl: './sign-step.scss'
})
export class SignStep extends BaseComponent implements OnDestroy {
  private signService = inject(SignService);
  private certState = inject(SignCertStateService);
  protected desktop = inject(DesktopService);

  thumbprint = input<string>('');
  certName = input<string>('');
  // Thư mục đã chọn ở bước cấu hình. Đây là NGUỒN DUY NHẤT của bước này: không còn ô kéo thả/nhập đường dẫn
  // ở đây nữa, vì cấu hình chữ ký được đặt trên chính các file của thư mục đó - đổi nguồn ở bước ký là mọi
  // vị trí vừa đặt trở thành vô nghĩa.
  initialSourcePath = input<string>('');
  // Cấu hình chữ ký chốt ở bước 2 (vị trí + cho đè nội dung); null = chưa qua bước cấu hình.
  config = input<ISignConfig | null>(null);
  reason = input<string>('');
  location = input<string>('');

  sourcePath = signal<string>('');

  constructor() {
    super();
    // Vào bước này là chạy kiểm luôn với đúng thư mục của bước cấu hình: người dùng không phải bấm gì thêm,
    // và không có đường nào để nguồn ở đây khác nguồn đã đặt chữ ký.
    effect(() => {
      const path = this.initialSourcePath();
      this.config();
      if (!path) return;

      untracked(() => {
        if (path !== this.sourcePath()) {
          this.sourcePath.set(path);
          this.startCheck();
          return;
        }
        this.checkPositions(Utils.normalizeFolderPath(path));
      });
    });
  }

  checking = signal<boolean>(false);
  signing = signal<boolean>(false);
  verifying = signal<boolean>(false);
  checkDone = signal<boolean>(false);
  // Tổng kết bước kiểm định dạng (validCount/invalidCount/total) để quyết gate ký + note bỏ qua.
  checkSummary = signal<IPdfFormatSummary | null>(null);
  percent = signal<number>(0);

  // Kết quả kiểm VỊ TRÍ chữ ký (khác kiểm ĐỊNH DẠNG ở trên): cấu hình có vừa trang của từng file không.
  positionChecking = signal<boolean>(false);
  positionCheck = signal<ISignPositionCheck | null>(null);

  fileRows = signal<ISignFileRow[]>([]);
  showSignDialog = signal<boolean>(false);
  // Dialog kết quả sau khi ký xong + dữ liệu tổng kết cả lô để hiển thị.
  showResultDialog = signal<boolean>(false);
  signResult = signal<ISignResult | null>(null);

  // Số file hợp lệ / không hợp lệ sau khi kiểm; dùng cho note "bỏ qua" và điều kiện mở nút Ký số.
  validCount = computed<number>(() => this.checkSummary()?.validCount ?? 0);
  invalidCount = computed<number>(() => this.checkSummary()?.invalidCount ?? 0);
  totalCount = computed<number>(() => this.checkSummary()?.totalFiles ?? 0);
  // Có file không hợp lệ nhưng vẫn còn file ký được -> hiện note (không chặn).
  hasSkippable = computed<boolean>(() => this.checkDone() && this.validCount() > 0 && this.invalidCount() > 0);

  // Các file sẽ bị chỉnh vị trí khi ký - chỉ những file này mới cần liệt kê, file vừa vặn không phải nói gì.
  adjustedFiles = computed(() => (this.positionCheck()?.files ?? []).filter((f) => !f.fitted));

  // Bảng sắp theo SỐ THỨ TỰ sau mã tài liệu (vd .1 < .2 < .10); nguồn cập nhật vẫn là fileRows.
  sortedFileRows = computed(() =>
    [...this.fileRows()].sort((a, b) => Utils.compareDocSeq(a.name, b.name))
  );

  // Enum verdict để dùng trong template.
  protected readonly EVerdict = EPdfFormatVerdict;

  /** Nhãn hiển thị của verdict (template không index được Record keyed enum nên bọc qua hàm). */
  verdictLabel(v: number): string {
    return PDF_FORMAT_VERDICT_LABELS[v as EPdfFormatVerdict] ?? '';
  }

  verdictSeverity(v: number, canSign?: boolean): 'success' | 'warn' | 'danger' {
    if (canSign) return v === EPdfFormatVerdict.Valid ? 'success' : 'warn';
    return PDF_FORMAT_VERDICT_SEVERITIES[v as EPdfFormatVerdict] ?? 'danger';
  }

  private sub?: Subscription;

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  canCheck(): boolean {
    return !!this.sourcePath().trim() && !this.checking() && !this.signing();
  }

  canSign(): boolean {
    return (
      !!this.thumbprint() &&
      this.checkDone() &&
      this.validCount() > 0 &&
      !this.checking() &&
      !this.signing() &&
      !this.verifying()
    );
  }

  // ===== Bước kiểm định dạng (SSE) + kiểm vị trí chữ ký =====
  startCheck(): void {
    const path = Utils.normalizeFolderPath(this.sourcePath());
    if (!path) return;

    this.checkPositions(path);
    this.sub?.unsubscribe();
    this.checking.set(true);
    this.checkDone.set(false);
    this.checkSummary.set(null);
    this.percent.set(0);
    this.fileRows.set([]);

    this.sub = this.signService.checkPdfFormat(path).subscribe({
      next: (ev) => this.handleCheckEvent(ev),
      error: () => {
        this.checking.set(false);
        this.messageError('Không kết nối được tới dịch vụ kiểm tra định dạng. Vui lòng thử lại sau.');
      },
      complete: () => this.checking.set(false)
    });
  }

  /**
   * Kiểm cấu hình chữ ký có vừa vặn với từng file không (tràn mép, trang không tồn tại, khối quá nhỏ).
   * KHÔNG chặn ký: BE tự dịch/phóng khối cho vừa, mục này chỉ để người dùng biết TRƯỚC file nào sẽ bị chỉnh.
   */
  private checkPositions(path: string): void {
    const config = this.config() ?? SIGN_DEFAULT_CONFIG;

    this.positionChecking.set(true);
    this.positionCheck.set(null);
    this.signService
      .checkPositions({
        sourcePath: path,
        useCustomPosition: config.useCustomPosition,
        positions: config.positions ?? null,
        fileOverrides: config.fileOverrides ?? null
      })
      .subscribe({
        next: (res) => {
          this.positionChecking.set(false);
          if (!this.isResponseSucceed(res)) {
            this.messageError(res.message || 'Không kiểm được vị trí chữ ký.');
            return;
          }
          this.positionCheck.set(res.data);
        },
        error: () => {
          this.positionChecking.set(false);
          this.messageError('Không kết nối được tới dịch vụ kiểm tra vị trí chữ ký. Vui lòng thử lại sau.');
        }
      });
  }

  private handleCheckEvent(ev: IPdfFormatProgress): void {
    if (typeof ev.percent === 'number') {
      this.percent.set(ev.percent);
    }

    if (ev.targetType === 'manifest') {
      this.fileRows.set(
        (ev.files ?? []).map((name) => ({
          name,
          checkStatus: 'processing' as const,
          signStatus: 'idle' as const
        }))
      );
    } else if (ev.targetType === 'file' && ev.file) {
      const f = ev.file;
      this.fileRows.update((rows) =>
        rows.map((r) =>
          r.name === f.fileName
            ? {
                ...r,
                checkStatus: f.canSign ? 'ok' : 'error',
                verdict: f.verdict,
                canSign: f.canSign,
                // File Valid thì message chỉ lặp lại nhãn badge nên bỏ trống cột Nguyên nhân.
                checkMessage: f.verdict === EPdfFormatVerdict.Valid ? undefined : f.message
              }
            : r
        )
      );
    } else if (ev.targetType === 'summary' && ev.summary) {
      // Lưu tổng kết -> validCount/invalidCount/hasSkippable + gate nút Ký số cùng đọc từ đây.
      this.checkSummary.set(ev.summary);
    }

    if (ev.completed) {
      this.percent.set(100);
      this.checkDone.set(true);
    }
  }

  // ===== Bước ký (SSE), khởi động từ popup =====
  onOpenSignDialog(): void {
    if (!this.canSign()) {
      this.messageWarning('Không có file PDF nào ký được. Vui lòng kiểm tra lại.');
      return;
    }
    if (this.certState.verifiedCert()?.thumbprint === this.thumbprint()) {
      this.showSignDialog.set(true);
      return;
    }

    this.verifying.set(true);
    this.signService.verifyToken(this.thumbprint()).subscribe({
      next: (res) => {
        this.verifying.set(false);
        if (!this.isResponseSucceed(res)) return;
        if (res.data?.valid) {
          this.certState.verifiedCert.set(res.data);
          this.showSignDialog.set(true);
        } else {
          this.certState.verifiedCert.set(null);
          this.messageError(res.data?.reason || 'Chứng thư số chưa sẵn sàng để ký. Vui lòng kiểm tra lại token/PIN.');
        }
      },
      error: () => {
        this.verifying.set(false);
        this.messageError('Không kiểm tra được chứng thư số. Vui lòng thử lại sau.');
      }
    });
  }

  onStartSign(payload: { reason: string; location: string }): void {
    const path = Utils.normalizeFolderPath(this.sourcePath());
    if (!path || !this.thumbprint()) return;

    // Cấu hình bước 2 áp cho CẢ LÔ. Chưa qua bước cấu hình -> dùng mặc định (hệ thống tự đặt vị trí,
    // vẫn co giãn theo khổ trang) thay vì bỏ trống, để BE luôn nhận đủ trường.
    const config = this.config() ?? SIGN_DEFAULT_CONFIG;

    const req: ISignRequest = {
      thumbprint: this.thumbprint(),
      sourcePath: path,
      reason: payload.reason || undefined,
      location: payload.location || undefined,
      useCustomPosition: config.useCustomPosition,
      allowOverlapContent: config.allowOverlapContent,
      positions: config.positions ?? null,
      fileOverrides: config.fileOverrides ?? null,
      fontFamily: this.config()?.fontFamily ?? null,
      fontSize: this.config()?.fontSize ?? null,
      textColor: this.config()?.textColor ?? null,
      sealImagePath: this.config()?.sealImagePath ?? null,
      wetSignatureImagePath: this.config()?.wetSignatureImagePath ?? null
    };

    this.sub?.unsubscribe();
    this.signing.set(true);
    this.percent.set(0);
    // Reset cột ký của mọi dòng, cho spinner xoay lại từ đầu mỗi file.
    this.fileRows.update((rows) =>
      rows.map((r) => ({
        ...r,
        signStatus: 'processing' as const,
        signMessage: undefined,
        signedTime: undefined,
        trustedTime: undefined
      }))
    );

    this.sub = this.signService.signFolder(req).subscribe({
      next: (ev) => this.handleSignEvent(ev),
      error: () => {
        this.signing.set(false);
        this.messageError('Không kết nối được tới dịch vụ ký số. Vui lòng thử lại sau.');
      },
      complete: () => this.signing.set(false)
    });
  }

  private handleSignEvent(ev: ISignProgress): void {
    if (typeof ev.percent === 'number') {
      this.percent.set(ev.percent);
    }

    if (ev.targetType === 'file' && ev.file) {
      const f = ev.file;
      // 3 trạng thái RIÊNG (không gộp skipped vào failed): Đã ký / Bỏ qua / Lỗi.
      // BE gate PDF/A phòng thủ nên vẫn có thể trả skipped ngay cả khi đã pre-check.
      this.fileRows.update((rows) =>
        rows.map((r) =>
          r.name === f.fileName
            ? {
                ...r,
                signStatus: f.success ? 'signed' : f.skipped ? 'skipped' : 'failed',
                // Bỏ qua: lý do = f.reason (không đạt PDF/A hai lớp). Lỗi: ưu tiên verifyReason rồi reason.
                signMessage: f.success
                  ? undefined
                  : f.skipped
                    ? f.reason || undefined
                    : f.verifyReason || f.reason || undefined,
                signedTime: f.signedTime,
                trustedTime: f.trustedTime,
                signatureCount: f.signatureCount,
                validSignatureCount: f.validSignatureCount,
                signatures: f.signatures
              }
            : r
        )
      );
    } else if (ev.targetType === 'summary' && ev.summary) {
      this.percent.set(100);
      // Lưu tổng kết + mở dialog kết quả (3 trạng thái tách riêng, có nút mở thư mục nguồn chứa file đã ký).
      this.signResult.set(ev.summary);
      this.showResultDialog.set(true);
    }
  }

  /** Mở thư mục kết quả: desktop -> Explorer; web-dev -> copy clipboard. */
  openFolder(path: string): void {
    if (!path) return;
    if (this.desktop.openPath(path)) return;
    navigator.clipboard?.writeText(path).then(
      () => this.messageSuccess('Đã sao chép đường dẫn thư mục kết quả vào clipboard.'),
      () => this.messageWarning('Không mở được thư mục. Đường dẫn: ' + path)
    );
  }
}
