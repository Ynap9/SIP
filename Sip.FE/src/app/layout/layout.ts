import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

import { Topbar } from './topbar';
import { Sidebar } from './sidebar';
import { Footer } from './footer';
import { LayoutService } from './service/layout.service';

@Component({
  selector: 'app-layout',
  imports: [CommonModule, RouterOutlet, Topbar, Sidebar, Footer, ToastModule, ConfirmDialogModule],
  templateUrl: './layout.html',
  styleUrl: './layout.css'
})
export class Layout {
  protected layoutService = inject(LayoutService);
  private router = inject(Router);

  constructor() {
    // Điều hướng xong -> đóng menu mobile
    this.router.events
      .pipe(
        filter((e) => e instanceof NavigationEnd),
        takeUntilDestroyed()
      )
      .subscribe(() => this.layoutService.hideMenu());
  }

  get containerClass() {
    const state = this.layoutService.state();
    return {
      'layout-static': true,
      'layout-static-inactive': state.staticMenuDesktopInactive,
      'layout-mobile-active': state.staticMenuMobileActive
    };
  }
}
