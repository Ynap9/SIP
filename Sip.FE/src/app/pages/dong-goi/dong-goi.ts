import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterOutlet } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { SelectButtonModule } from 'primeng/selectbutton';

import { Breadcrumb } from '../../shared/components/breadcrumb/breadcrumb';
import { DongGoiStateService } from './dong-goi-state.service';

@Component({
  selector: 'app-dong-goi',
  imports: [FormsModule, RouterOutlet, SelectButtonModule, Breadcrumb],
  templateUrl: './dong-goi.html',
  styleUrl: './dong-goi.scss'
})
export class DongGoi implements OnInit {
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private state = inject(DongGoiStateService);

  home: MenuItem = { icon: 'pi pi-home', routerLink: '/' };
  breadcrumbItems: MenuItem[] = [{ label: 'Kiểm tra & đóng gói' }];

  // single -> action Check (dong-goi/check) ; multi -> action Multi (dong-goi/multi)
  modeOptions = [
    { label: 'Một hồ sơ', value: 'single' },
    { label: 'Nhiều hồ sơ', value: 'multi' }
  ];
  // Mode lưu ở service -> quay lại trang vẫn đúng chế độ trước đó.
  mode = this.state.mode;

  ngOnInit(): void {
    // Mở đúng action theo chế độ đã lưu (mặc định 1 hồ sơ)
    this.router.navigate([this.mode() === 'multi' ? 'multi' : 'check'], { relativeTo: this.route });
  }

  onModeChange(value: 'single' | 'multi'): void {
    this.state.mode.set(value);
    this.router.navigate([value === 'multi' ? 'multi' : 'check'], { relativeTo: this.route });
  }
}
