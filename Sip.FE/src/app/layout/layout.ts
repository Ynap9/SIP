import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';

import { Topbar } from './topbar';
import { Footer } from './footer';
import { LayoutService } from './service/layout.service';

@Component({
  selector: 'app-layout',
  imports: [RouterOutlet, Topbar, Footer, ToastModule, ConfirmDialogModule],
  templateUrl: './layout.html',
  styleUrl: './layout.css'
})
export class Layout {
  // Không đọc gì từ service, nhưng đây là chỗ DUY NHẤT tạo ra nó: LayoutService dựng cỡ zoom và --app-vh
  // ngay trong constructor. Service providedIn:'root' chỉ được khởi tạo khi có người inject, nên bỏ dòng
  // này là toàn app mất khả năng thu nhỏ theo cửa sổ.
  protected readonly layoutService = inject(LayoutService);
}
