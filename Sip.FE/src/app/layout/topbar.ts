import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { LayoutService } from './service/layout.service';

@Component({
  selector: 'app-topbar',
  imports: [RouterLink],
  template: `
    <div class="layout-topbar">
      <div class="layout-topbar-logo-container">
        <button
          type="button"
          class="layout-menu-button"
          aria-label="Ẩn/hiện menu"
          (click)="layoutService.onMenuToggle()"
        >
          <i class="pi pi-bars text-lg"></i>
        </button>
        <a class="layout-topbar-logo" routerLink="/">
          <i class="pi pi-box"></i>
          <span class="flex flex-col leading-tight">
            <span>SIPPACK</span>
            <span class="text-[0.7rem] font-normal text-slate-400">Hệ thống đóng gói chuẩn SIP</span>
          </span>
        </a>
      </div>
    </div>
  `
})
export class Topbar {
  protected layoutService = inject(LayoutService);
}
