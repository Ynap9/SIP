import { Routes } from '@angular/router';
import { DongGoi } from './dong-goi';
import { Check } from './check/check';

// Router cho tính năng Kiểm tra & đóng gói (lazy-load qua app.routes.ts)
// Check là 1 action con: route dong-goi/check
export const dongGoiRoutes: Routes = [
  {
    path: '',
    component: DongGoi,
    data: { title: 'Kiểm tra & đóng gói tài liệu theo chuẩn SIP' },
    children: [
      { path: 'check', component: Check }
    ]
  }
];
