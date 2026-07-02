import { Routes } from '@angular/router';
import { BocTach } from './boc-tach';

// Router cho tính năng Bóc tách dữ liệu (lazy-load qua app.routes.ts)
export const bocTachRoutes: Routes = [
  { path: '', component: BocTach, data: { title: 'Bóc tách dữ liệu', reuse: true } }
];
