import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

interface NavItem {
  label: string;
  link: string;
  icon: string;
}

@Component({
  selector: 'app-menu',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="layout-menu flex flex-col gap-1 p-3">
      @for (item of navItems; track item.link) {
        <a
          [routerLink]="item.link"
          routerLinkActive="bg-indigo-50 text-indigo-700 font-semibold"
          [routerLinkActiveOptions]="{ exact: item.link === '/' }"
          class="group flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium text-slate-600 transition-colors hover:bg-slate-100 hover:text-slate-900"
        >
          <i [class]="item.icon" class="text-base text-slate-400 group-hover:text-slate-600"></i>
          <span>{{ item.label }}</span>
        </a>
      }
    </nav>
  `
})
export class Menu {
  protected readonly navItems: NavItem[] = [
    { label: 'Trang chủ', link: '/', icon: 'pi pi-home' },
    { label: 'Kiểm tra & đóng gói', link: '/dong-goi', icon: 'pi pi-box' },
    { label: 'Bóc tách dữ liệu', link: '/boc-tach', icon: 'pi pi-sitemap' },
    { label: 'Cấu hình', link: '/cau-hinh', icon: 'pi pi-cog' }
  ];
}
