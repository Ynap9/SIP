import { Component, inject, signal } from '@angular/core';
import { MenuItem } from 'primeng/api';
import { StepsModule } from 'primeng/steps';

import { SharedImports } from '../../shared/import.shared';
import { BaseComponent } from '../../shared/components/base/base-component';
import { Breadcrumb } from '../../shared/components/breadcrumb/breadcrumb';
import { DesktopService } from '../../services/desktop.service';
import { ISignConfig, ITokenVerify } from '../../models/sign.models';
import { CertStep } from './cert-step/cert-step';
import { ConfigStep } from './config-step/config-step';
import { SignStep } from './sign-step/sign-step';

@Component({
  selector: 'app-ky-so',
  // StepsModule không nằm trong SharedImports -> import trực tiếp tại trang.
  imports: [...SharedImports, StepsModule, Breadcrumb, CertStep, ConfigStep, SignStep],
  templateUrl: './ky-so.html',
  styleUrl: './ky-so.scss'
})
export class KySo extends BaseComponent {
  protected desktop = inject(DesktopService);

  home: MenuItem = { icon: 'pi pi-home', routerLink: '/' };
  breadcrumbItems: MenuItem[] = [{ label: 'Ký số' }];

  // Chỉ báo 3 bước; cho phép click chọn bước (không read-only). Các bước sau tự cảnh báo nếu chưa đủ điều kiện.
  steps: MenuItem[] = [{ label: 'Chọn chứng thư số' }, { label: 'Cấu hình chữ ký' }, { label: 'Ký số' }];
  activeStep = signal<number>(0);

  // Chứng thư số đã kiểm token thành công ở bước 1 -> dùng thumbprint để ký ở bước 3.
  verifiedCert = signal<ITokenVerify | null>(null);

  // Cấu hình chữ ký chốt ở bước 2 (vị trí + số trang + cho đè nội dung), áp cho cả lô ký ở bước 3.
  signConfig = signal<ISignConfig | null>(null);

  // Thư mục nguồn chọn ở bước 2 để xem trước -> bước 3 ký đúng thư mục đó, không phải chọn lại.
  sourcePath = signal<string>('');

  /** Click 1 bước trên stepper -> chuyển thẳng sang bước đó (bước 2 tự cảnh báo nếu chưa xong bước 1). */
  onStepChange(index: number): void {
    this.activeStep.set(index);
  }

  /** Bước 1 kiểm token OK -> lưu cert và TỰ ĐỘNG sang bước 2 (cấu hình chữ ký). */
  onVerified(cert: ITokenVerify): void {
    this.verifiedCert.set(cert);
    this.activeStep.set(1);
  }

  /** Bước 2 chốt cấu hình (tự cấu hình hoặc lưu vị trí đã chọn) -> sang bước ký. */
  onConfigured(config: ISignConfig): void {
    this.signConfig.set(config);
    this.activeStep.set(2);
  }

  /** Quay lại bước 1 để đổi chứng thư số. */
  backToCert(): void {
    this.activeStep.set(0);
  }

  /** Quay lại bước 2 để sửa cấu hình chữ ký. */
  backToConfig(): void {
    this.activeStep.set(1);
  }
}
