import { Routes } from '@angular/router';
import { Config } from './config';
import { HoSoVanBan } from './ho-so-van-ban/ho-so-van-ban';
import { ChungThuSo } from './chung-thu-so/chung-thu-so';
import { TemplateChuKy } from './template-chu-ky/template-chu-ky';
import { TemplateConfig } from './template-chu-ky/template-config/template-config';

// Router cho tính năng Cấu hình (lazy-load qua app.routes.ts)
export const configRoutes: Routes = [
  {
    path: '',
    component: Config,
    data: { title: 'Cấu hình' },
    children: [
      { path: 'ho-so-van-ban', component: HoSoVanBan, data: { reuse: true } },
      { path: 'chung-thu-so', component: ChungThuSo, data: { reuse: true } },
      { path: 'template-chu-ky', component: TemplateChuKy },
      { path: 'template-chu-ky/:id', component: TemplateConfig }
    ]
  }
];
