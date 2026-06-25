import { Routes } from '@angular/router';
import { Config } from './config';

// Router cho tính năng Cấu hình (lazy-load qua app.routes.ts)
export const configRoutes: Routes = [
  { path: '', component: Config, data: { title: 'Cấu hình hồ sơ/loại tài liệu' } }
];
