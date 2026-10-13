import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

interface NavItem {
  label: string;
  icon: string;
  link: string;
}

@Component({
  selector: 'app-menu',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="layout-topbar-nav">
      @for (item of navItems; track item.link) {
        <a
          [routerLink]="item.link"
          routerLinkActive="is-active"
          [routerLinkActiveOptions]="{ exact: item.link === '/' }"
          class="layout-topbar-nav-item"
          draggable="false"
        >
          <i class="pi" [class]="item.icon"></i>
          <span>{{ item.label }}</span>
        </a>
      }
    </nav>
  `
})
export class Menu {
  protected readonly navItems: NavItem[] = [
    { label: 'Trang chủ', icon: 'pi-home', link: '/' },
    { label: 'Kiểm tra & đóng gói', icon: 'pi-box', link: '/dong-goi' },
    { label: 'Ký số', icon: 'pi-verified', link: '/ky-so' },
    { label: 'Bóc tách dữ liệu', icon: 'pi-sitemap', link: '/boc-tach' },
    { label: 'Cấu hình', icon: 'pi-cog', link: '/cau-hinh' }
  ];
}
