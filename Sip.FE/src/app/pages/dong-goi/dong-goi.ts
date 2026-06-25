import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterOutlet } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { SelectButtonModule } from 'primeng/selectbutton';

import { Breadcrumb } from '../../shared/components/breadcrumb/breadcrumb';
import { Empty } from '../empty/empty';

@Component({
  selector: 'app-dong-goi',
  imports: [FormsModule, RouterOutlet, SelectButtonModule, Breadcrumb, Empty],
  templateUrl: './dong-goi.html',
  styleUrl: './dong-goi.scss'
})
export class DongGoi implements OnInit {
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  home: MenuItem = { icon: 'pi pi-home', routerLink: '/' };
  breadcrumbItems: MenuItem[] = [{ label: 'Kiểm tra & đóng gói' }];

  // single -> action Check (route dong-goi/check) ; multi -> page Empty (render inline)
  modeOptions = [
    { label: 'Một hồ sơ', value: 'single' },
    { label: 'Nhiều hồ sơ', value: 'multi' }
  ];
  mode = signal<'single' | 'multi'>('single');

  ngOnInit(): void {
    // Mặc định mở action Check
    this.router.navigate(['check'], { relativeTo: this.route });
  }

  onModeChange(value: 'single' | 'multi'): void {
    this.mode.set(value);
    if (value === 'single') {
      this.router.navigate(['check'], { relativeTo: this.route });
    }
  }
}
