import { Routes } from '@angular/router';
import { KySo } from './ky-so';

// Router cho tính năng Ký số (lazy-load qua app.routes.ts)
export const kySoRoutes: Routes = [
  { path: '', component: KySo, data: { title: 'Ký số tài liệu', reuse: true } }
];
