import { Component, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { SelectButtonModule } from 'primeng/selectbutton';
import { filter } from 'rxjs';

import { Breadcrumb } from '../../shared/components/breadcrumb/breadcrumb';

const CONFIG_TAB_DEFAULT = 'ho-so-van-ban';

@Component({
  selector: 'app-config',
  imports: [FormsModule, RouterOutlet, SelectButtonModule, Breadcrumb],
  templateUrl: './config.html',
  styleUrl: './config.scss'
})
export class Config implements OnInit {
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  home: MenuItem = { icon: 'pi pi-home', routerLink: '/' };
  breadcrumbItems: MenuItem[] = [{ label: 'Cấu hình' }];

  tabOptions = [
    { label: 'Hồ sơ / Văn bản', value: CONFIG_TAB_DEFAULT },
    { label: 'Chứng thư số', value: 'chung-thu-so' },
    { label: 'Template chữ ký', value: 'template-chu-ky' }
  ];
  tab = signal<string>(CONFIG_TAB_DEFAULT);

  constructor() {
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed()
      )
      .subscribe(() => this.syncTab());
  }

  ngOnInit(): void {
    this.syncTab();
  }

  onTabChange(value: string): void {
    this.router.navigate([value], { relativeTo: this.route });
  }

  private syncTab(): void {
    const path = this.route.firstChild?.snapshot.url[0]?.path;
    if (!path) {
      this.router.navigate([CONFIG_TAB_DEFAULT], { relativeTo: this.route, replaceUrl: true });
      return;
    }
    this.tab.set(path);
  }
}
