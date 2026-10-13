import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { MenuItem } from 'primeng/api';
import { TagModule } from 'primeng/tag';

import { SharedImports } from '../../shared/import.shared';
import { BaseComponent } from '../../shared/components/base/base-component';
import { Breadcrumb } from '../../shared/components/breadcrumb/breadcrumb';
import { Utils } from '../../shared/utils';
import { DesktopService } from '../../services/desktop.service';
import { SignService } from '../../services/sign.service';
import { SignTemplateService } from '../../services/sign-template.service';
import { SignCertStateService } from '../../services/sign-cert-state.service';
import { ISignCert, ISignConfig } from '../../models/sign.models';
import { IViewSignTemplate } from '../../models/sign-template.models';
import { ESignTemplatePositionKind } from '../../shared/constants/sign.constants';
import { SignStep } from './sign-step/sign-step';

const TEMPLATE_LIST_URL = '/cau-hinh/template-chu-ky';

@Component({
  selector: 'app-ky-so',
  imports: [...SharedImports, TagModule, Breadcrumb, SignStep],
  templateUrl: './ky-so.html',
  styleUrl: './ky-so.scss'
})
export class KySo extends BaseComponent implements OnInit {
  private signService = inject(SignService);
  private signTemplateService = inject(SignTemplateService);
  private certState = inject(SignCertStateService);
  protected desktop = inject(DesktopService);

  home: MenuItem = { icon: 'pi pi-home', routerLink: '/' };
  breadcrumbItems: MenuItem[] = [{ label: 'Ký số' }];

  templates = signal<IViewSignTemplate[]>([]);
  templateLoading = signal<boolean>(false);
  templateId = signal<number | null>(null);

  certs = signal<ISignCert[]>([]);
  certLoading = signal<boolean>(false);
  thumbprint = signal<string | null>(null);
  verifying = signal<boolean>(false);

  isDragOver = signal<boolean>(false);
  pathInput = signal<string>('');
  sourcePath = signal<string>('');

  selectedTemplate = computed<IViewSignTemplate | null>(
    () => this.templates().find((template) => template.id === this.templateId()) ?? null
  );

  certName = computed<string>(
    () =>
      this.certs().find((cert) => cert.thumbprint === this.thumbprint())?.commonName ||
      this.selectedTemplate()?.tenChungThu ||
      ''
  );

  isVerified = computed<boolean>(
    () => !!this.thumbprint() && this.certState.verifiedCert()?.thumbprint === this.thumbprint()
  );

  signConfig = computed<ISignConfig | null>(() => {
    const template = this.selectedTemplate();
    if (!template) return null;

    const positions = (template.positions ?? [])
      .filter(
        (position) =>
          position.kind === ESignTemplatePositionKind.ChuKy ||
          (position.kind === ESignTemplatePositionKind.DauDo && !!template.anhDauDoPath) ||
          (position.kind === ESignTemplatePositionKind.ChuKyTuoi && !!template.anhChuKyTuoiPath)
      )
      .map(({ kind, pageNumber, xRatio, yRatio, widthRatio, heightRatio }) => ({
        kind,
        pageNumber,
        xRatio,
        yRatio,
        widthRatio,
        heightRatio
      }));

    return {
      useCustomPosition: positions.length > 0,
      allowOverlapContent: false,
      positions: positions.length > 0 ? positions : null,
      fileOverrides: null,
      fontFamily: template.fontChuKySo ?? null,
      fontSize: template.coChuKySo ?? null,
      textColor: template.mauChuKySo ?? null,
      sealImagePath: template.anhDauDoPath ?? null,
      wetSignatureImagePath: template.anhChuKyTuoiPath ?? null
    };
  });

  override ngOnInit(): void {
    this.loadTemplates();
    this.loadCertificates();
  }

  override onRouteActivated(): void {
    this.loadTemplates();
  }

  loadTemplates(): void {
    this.templateLoading.set(true);
    this.signTemplateService.findPaging({ pageNumber: this.START_PAGE_NUMBER, pageSize: -1 }).subscribe({
      next: (res) => {
        this.templates.set(this.isResponseSucceed(res) ? (res.data.items ?? []) : []);
        if (!this.selectedTemplate()) {
          this.templateId.set(null);
        }
        this.templateLoading.set(false);
      },
      error: () => {
        this.messageError('Không tải được danh sách template chữ ký. Vui lòng thử lại sau.');
        this.templates.set([]);
        this.templateLoading.set(false);
      }
    });
  }

  loadCertificates(): void {
    this.certLoading.set(true);
    this.signService.getCertificates(true).subscribe({
      next: (res) => {
        this.certs.set(this.isResponseSucceed(res) ? (res.data ?? []) : []);
        this.certLoading.set(false);
      },
      error: () => {
        this.messageError('Không thể đọc chứng thư số trên máy. Vui lòng thử lại sau.');
        this.certs.set([]);
        this.certLoading.set(false);
      }
    });
  }

  onTemplateChange(id: number | null): void {
    this.templateId.set(id);
    this.thumbprint.set(this.selectedTemplate()?.thumbprint || null);
  }

  onCertChange(thumbprint: string | null): void {
    this.thumbprint.set(thumbprint);
  }

  verify(): void {
    const thumbprint = this.thumbprint();
    if (!thumbprint || this.verifying()) return;

    this.verifying.set(true);
    this.signService.verifyToken(thumbprint).subscribe({
      next: (res) => {
        this.verifying.set(false);
        if (!this.isResponseSucceed(res)) return;
        if (res.data?.valid) {
          this.certState.verifiedCert.set(res.data);
          this.messageSuccess('Chứng thư số đã sẵn sàng để ký.');
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

  goToTemplates(): void {
    this.router.navigateByUrl(TEMPLATE_LIST_URL);
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(false);
  }

  onDropFolder(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(false);
    const file = event.dataTransfer?.files?.[0] as (File & { path?: string }) | undefined;
    if (file?.path) {
      this.applySource(file.path);
    } else {
      this.messageWarning('Không lấy được đường dẫn thư mục. Vui lòng dán đường dẫn vào ô bên dưới.');
    }
  }

  async chooseFolder(): Promise<void> {
    const path = await this.desktop.pickFolder();
    if (path) {
      this.applySource(path);
    }
  }

  applyPathInput(): void {
    if (!this.pathInput().trim()) return;
    this.applySource(this.pathInput());
  }

  private applySource(path: string): void {
    const normalized = Utils.normalizeFolderPath(path);
    this.pathInput.set(normalized);
    this.sourcePath.set(normalized);
  }
}
