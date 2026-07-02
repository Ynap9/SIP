import { Routes } from '@angular/router';
import { DongGoi } from './dong-goi';
import { Check } from './check/check';
import { Multi } from './multi/multi';

// Router cho tính năng Kiểm tra & đóng gói (lazy-load qua app.routes.ts)
// 2 action con: dong-goi/check (1 hồ sơ) và dong-goi/multi (nhiều hồ sơ)
export const dongGoiRoutes: Routes = [
  {
    path: '',
    component: DongGoi,
    data: { title: 'Kiểm tra & đóng gói tài liệu theo chuẩn SIP' },
    children: [
      { path: 'check', component: Check, data: { reuse: true } },
      { path: 'multi', component: Multi, data: { reuse: true } }
    ]
  }
];
